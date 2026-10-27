using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Velqora.Infrastructure;
namespace Velqora.Api;
[Authorize]
public sealed class SocHub : Hub { }
public sealed class LivePublisher(IServiceScopeFactory scopes, IHubContext<SocHub> hub, ILogger<LivePublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        string? previous = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SocDb>();
                var events = await db.Events.CountAsync(ct);
                var alerts = await db.Alerts.CountAsync(ct);
                var incidents = await db.Incidents.CountAsync(ct);
                var outbox = await db.Outbox.CountAsync(x => x.PublishedAt == null, ct);
                var current = $"{events}:{alerts}:{incidents}:{outbox}";
                if (current != previous)
                {
                    await hub.Clients.All.SendAsync("changed", new { events, alerts, incidents, outbox, at = DateTimeOffset.UtcNow }, ct);
                    previous = current;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Live updates unavailable; retrying"); }
            await Task.Delay(2000, ct);
        }
    }
}
