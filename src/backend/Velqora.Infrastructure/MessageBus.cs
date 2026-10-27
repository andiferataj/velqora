using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
namespace Velqora.Infrastructure;

public sealed record QueueStats(uint Pending, uint Consumers, uint DeadLetters);
public sealed class MessageBus(IConfiguration config) : IAsyncDisposable
{
    public const string Queue = "velqora.detection";
    public const string DeadQueue = "velqora.detection.dead";
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;

    public async Task<IChannel> ChannelAsync(bool confirm, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (connection is null || !connection.IsOpen)
            {
                if (connection is not null) await connection.DisposeAsync();
                connection = await new ConnectionFactory
                {
                    Uri = new Uri(config["RabbitMq"] ?? throw new InvalidOperationException("RabbitMq is required.")),
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                    AutomaticRecoveryEnabled = true, NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                    ClientProvidedName = "velqora"
                }.CreateConnectionAsync(ct);
            }
        }
        finally { gate.Release(); }
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(confirm, confirm), ct);
        await channel.ExchangeDeclareAsync("velqora.events", ExchangeType.Direct, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync("velqora.dead", ExchangeType.Direct, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadQueue, true, false, false, cancellationToken: ct);
        await channel.QueueBindAsync(DeadQueue, "velqora.dead", "event", cancellationToken: ct);
        await channel.QueueDeclareAsync(Queue, true, false, false, new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum", ["x-delivery-limit"] = 5,
            ["x-dead-letter-exchange"] = "velqora.dead", ["x-dead-letter-routing-key"] = "event"
        }, cancellationToken: ct);
        await channel.QueueBindAsync(Queue, "velqora.events", "event", cancellationToken: ct);
        return channel;
    }

    public static ValueTask PublishAsync(IChannel channel, Guid eventId, CancellationToken ct) =>
        channel.BasicPublishAsync("velqora.events", "event", true, new BasicProperties
        {
            Persistent = true, MessageId = eventId.ToString(), ContentType = "text/plain",
            Headers = new Dictionary<string, object?> { ["traceparent"] = System.Diagnostics.Activity.Current?.Id ?? "" }
        }, Encoding.UTF8.GetBytes(eventId.ToString()), ct);

    public async Task<QueueStats> InspectAsync(CancellationToken ct)
    {
        await using var ch = await ChannelAsync(false, ct);
        var main = await ch.QueueDeclarePassiveAsync(Queue, ct);
        var dead = await ch.QueueDeclarePassiveAsync(DeadQueue, ct);
        return new QueueStats(main.MessageCount, main.ConsumerCount, dead.MessageCount);
    }
    public async Task<int> ReplayAsync(CancellationToken ct)
    {
        await using var ch = await ChannelAsync(true, ct);
        int count = 0;
        while (count < 100)
        {
            var message = await ch.BasicGetAsync(DeadQueue, false, ct);
            if (message == null) break;
            if (!Guid.TryParse(Encoding.UTF8.GetString(message.Body.Span), out var id))
            {
                await ch.BasicNackAsync(message.DeliveryTag, false, true, ct);
                break;
            }
            await PublishAsync(ch, id, ct);
            await ch.BasicAckAsync(message.DeliveryTag, false, ct);
            count++;
        }
        return count;
    }
    public async ValueTask DisposeAsync()
    {
        if (connection != null) await connection.DisposeAsync();
        gate.Dispose();
    }
}
