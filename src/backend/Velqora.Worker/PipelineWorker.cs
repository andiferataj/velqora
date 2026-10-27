using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Velqora.Core;
using Velqora.Infrastructure;
namespace Velqora.Worker;

public sealed class PipelineWorker(IServiceScopeFactory scopes, MessageBus bus, ILogger<PipelineWorker> logger) : BackgroundService
{
    private volatile bool consuming;
    private DateTimeOffset lastMetrics;
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(DispatchLoop(stoppingToken), ConsumeLoop(stoppingToken));

    private async Task DispatchLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var healthy = consuming;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SocDb>();
                await using (var tx = await db.Database.BeginTransactionAsync(ct))
                {
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73005)", ct);
                    var batch = await db.Outbox.Where(x => x.PublishedAt == null && x.NextAttemptAt <= DateTimeOffset.UtcNow)
                        .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
                    if (batch.Count > 0)
                    {
                        await using var channel = await bus.ChannelAsync(true, ct);
                        foreach (var message in batch)
                        {
                            using var activity = Telemetry.Source.StartActivity("outbox.publish", ActivityKind.Producer, message.TraceParent);
                            activity?.SetTag("event.id", message.EventId);
                            message.Attempts++;
                            try
                            {
                                await MessageBus.PublishAsync(channel, message.EventId, ct);
                                message.PublishedAt = DateTimeOffset.UtcNow; message.LastError = null;
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                message.LastError = ex.GetType().Name;
                                message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, Math.Min(message.Attempts, 6))));
                                Telemetry.Failures.Add(1); healthy = false;
                                logger.LogWarning(ex, "Outbox publish failed for {EventId}; retained for retry", message.EventId);
                                break;
                            }
                        }
                    }
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                }
                await scope.ServiceProvider.GetRequiredService<OperationsService>().SimulateAsync(ct);
                await scope.ServiceProvider.GetRequiredService<OperationsService>().AutomateAsync(ct);
                await scope.ServiceProvider.GetRequiredService<DetectionService>().DetectTelemetryLossAsync(ct);
                var heartbeat = await db.Heartbeats.SingleOrDefaultAsync(x => x.Service == "pipeline-worker", ct);
                if (heartbeat == null) { heartbeat = new() { Service = "pipeline-worker" }; db.Heartbeats.Add(heartbeat); }
                heartbeat.LastSeenAt = DateTimeOffset.UtcNow; heartbeat.State = healthy ? "Healthy" : "Degraded";
                if (DateTimeOffset.UtcNow - lastMetrics > TimeSpan.FromSeconds(5))
                {
                    lastMetrics = DateTimeOffset.UtcNow;
                    Interlocked.Exchange(ref Telemetry.PendingOutbox, await db.Outbox.CountAsync(x => x.PublishedAt == null, ct));
                    var queues = await bus.InspectAsync(ct);
                    Interlocked.Exchange(ref Telemetry.PendingQueue, queues.Pending);
                    Interlocked.Exchange(ref Telemetry.DeadLetters, queues.DeadLetters);
                }
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Worker cycle failed; durable state retained");
                Telemetry.Failures.Add(1);
            }
            await Task.Delay(200, ct);
        }
    }

    private async Task ConsumeLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var channel = await bus.ChannelAsync(false, ct);
                await channel.BasicQosAsync(0, 1, false, ct);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, args) =>
                {
                    try
                    {
                        var parent = args.BasicProperties.Headers?.TryGetValue("traceparent", out var trace) == true && trace is byte[] bytes
                            ? Encoding.UTF8.GetString(bytes) : null;
                        using var activity = Telemetry.Source.StartActivity("queue.consume", ActivityKind.Consumer, parent);
                        if (!Guid.TryParse(Encoding.UTF8.GetString(args.Body.Span), out var eventId))
                        {
                            await channel.BasicNackAsync(args.DeliveryTag, false, false, ct); return;
                        }
                        await using var scope = scopes.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<DetectionService>().ProcessAsync(eventId, ct);
                        await channel.BasicAckAsync(args.DeliveryTag, false, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Consumer failed; quorum delivery limit routes persistent failures to dead letters");
                        Telemetry.Failures.Add(1);
                        await Task.Delay(500, ct);
                        if (channel.IsOpen) await channel.BasicNackAsync(args.DeliveryTag, false, true, ct);
                    }
                };
                await channel.BasicConsumeAsync(MessageBus.Queue, false, consumer, ct);
                consuming = true;
                while (channel.IsOpen && !ct.IsCancellationRequested) await Task.Delay(1000, ct);
                consuming = false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                consuming = false;
                logger.LogWarning(ex, "Consumer disconnected; reconnecting");
                await Task.Delay(3000, ct);
            }
        }
    }
}
