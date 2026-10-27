using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Velqora.Core;
using Velqora.Infrastructure;
namespace Velqora.Api;

public static class ReportingEndpoints
{
    public static void MapReporting(this WebApplication app)
    {
        app.MapGet("/api/analytics", async (SocDb db, int? days, CancellationToken ct) =>
        {
            var range = Math.Clamp(days ?? 7, 1, 90);
            var since = DateTimeOffset.UtcNow.AddDays(-range);
            var events = db.Events.Where(x => x.OccurredAt >= since);
            var alerts = db.Alerts.Where(x => x.DetectedAt >= since);
            var incidents = db.Incidents.Where(x => x.CreatedAt >= since);
            var types = await events.GroupBy(x => x.Type).Select(g => new { name = g.Key, count = g.Count() }).ToListAsync(ct);
            var severity = await alerts.GroupBy(x => x.Severity).Select(g => new { name = g.Key.ToString(), count = g.Count() }).ToListAsync(ct);
            var trend = await ReportingQueries.DailyActivityAsync(db, since, ct);
            var closed = await incidents.Where(x => x.ResolvedAt != null).Select(x => new { x.CreatedAt, x.ResolvedAt }).ToListAsync(ct);
            var alertCount = await alerts.CountAsync(ct);
            var syntheticCount = await alerts.CountAsync(x => x.Synthetic, ct);
            var benignIds = await db.ScenarioRuns.Where(x => x.Scenario == "benign-baseline").Select(x => x.Id).ToListAsync(ct);
            var falsePositives = await alerts.CountAsync(x => x.ScenarioRunId != null && benignIds.Contains(x.ScenarioRunId.Value), ct);
            return Results.Ok(new
            {
                days = range, eventCount = await events.CountAsync(ct), alertCount,
                openAlerts = await db.Alerts.CountAsync(x => x.Status != AlertStatus.Resolved && x.Status != AlertStatus.FalsePositive, ct),
                activeIncidents = await db.Incidents.CountAsync(x => x.Status != IncidentStatus.Resolved && x.Status != IncidentStatus.Closed, ct),
                assetCount = await db.Assets.CountAsync(ct),
                enabledRules = await db.Rules.CountAsync(x => x.Enabled, ct),
                meanResolutionMinutes = closed.Count == 0 ? (double?)null : closed.Average(x => (x.ResolvedAt!.Value - x.CreatedAt).TotalMinutes),
                syntheticPrecision = syntheticCount == 0 ? (double?)null : (syntheticCount - falsePositives) / (double)syntheticCount,
                syntheticAlerts = syntheticCount, labeledBenignAlerts = falsePositives, eventTypes = types, severities = severity, trend
            });
        }).RequireAuthorization();
        app.MapGet("/api/platform", async (SocDb db, MessageBus bus, IConnectionMultiplexer redis, CancellationToken ct) =>
        {
            object? broker = null; string? brokerError = null;
            try { broker = await bus.InspectAsync(ct); } catch (Exception ex) { brokerError = ex.GetType().Name; }
            bool redisReady;
            try { await redis.GetDatabase().PingAsync(); redisReady = true; } catch (RedisException) { redisReady = false; }
            var heartbeat = await db.Heartbeats.AsNoTracking().SingleOrDefaultAsync(x => x.Service == "pipeline-worker", ct);
            return Results.Ok(new
            {
                at = DateTimeOffset.UtcNow, postgres = await db.Database.CanConnectAsync(ct),
                redis = redisReady, broker, brokerError,
                worker = new { ready = heartbeat != null && heartbeat.LastSeenAt > DateTimeOffset.UtcNow.AddSeconds(-20) && heartbeat.State == "Healthy", heartbeat },
                outboxPending = await db.Outbox.CountAsync(x => x.PublishedAt == null, ct),
                outboxRetries = await db.Outbox.CountAsync(x => x.PublishedAt == null && x.Attempts > 0, ct),
                processedEvents = await db.Processed.CountAsync(ct),
                oldestPending = await db.Outbox.Where(x => x.PublishedAt == null).MinAsync(x => (DateTimeOffset?)x.CreatedAt, ct)
            });
        }).RequireAuthorization();
        app.MapGet("/api/audit", async (SocDb db, string? resource, int? page, int? size, CancellationToken ct) =>
        {
            var query = db.Audit.AsNoTracking();
            if (!string.IsNullOrEmpty(resource)) query = query.Where(x => x.Resource == resource);
            return Results.Ok(await ApiHelpers.PageAsync(query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id), page, size, ct));
        }).RequireAuthorization("Admin");
        app.MapPost("/api/platform/replay-dead-letters", async (MessageBus bus, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            var replayed = await bus.ReplayAsync(ct);
            db.Record(ctx.User.Actor(), "queue.dead-letters-replayed", "detection", $"count={replayed}");
            await db.SaveChangesAsync(ct); return Results.Ok(new { replayed });
        }).RequireAuthorization("Admin");
    }
}
