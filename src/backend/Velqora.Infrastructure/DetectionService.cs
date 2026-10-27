using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Velqora.Core;
namespace Velqora.Infrastructure;

public sealed class DetectionService(SocDb db, IConnectionMultiplexer redis, ILogger<DetectionService> logger)
{
    public async Task ProcessAsync(Guid id, CancellationToken ct)
    {
        using var activity = Telemetry.Source.StartActivity("detect.event");
        activity?.SetTag("event.id", id);
        var watch = Stopwatch.StartNew();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73002)", ct);
        if (await db.Processed.AnyAsync(x => x.EventId == id, ct)) return;
        var trigger = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("Event not found.", 404);
        var enabled = await db.Rules.Where(x => x.Enabled && x.Kind != RuleKind.TelemetryLoss).ToListAsync(ct);
        var rules = enabled.Where(r => r.Kind switch
        {
            RuleKind.Threshold => r.EventType == trigger.Type,
            RuleKind.FailureThenSuccess => trigger.Type is "auth.failure" or "auth.success",
            RuleKind.PrivilegeMisuse => trigger.Type == "privilege.change",
            _ => false
        }).ToList();
        // Re-evaluate later anchors when an older event arrives, preserving event-time semantics.
        var maxWindow = rules.Count > 0 ? rules.Max(x => x.WindowSeconds) : 300;
        var from = trigger.OccurredAt.AddSeconds(-maxWindow);
        var to = trigger.OccurredAt.AddSeconds(maxWindow);
        var candidates = rules.Count == 0 ? new List<SecurityEvent>() : await db.Events.Where(x => x.Device == trigger.Device && x.Account == trigger.Account &&
            x.Synthetic == trigger.Synthetic && x.ScenarioRunId == trigger.ScenarioRunId &&
            x.OccurredAt >= from && x.OccurredAt <= to).ToListAsync(ct);
        int alerts = 0;
        foreach (var rule in rules)
        foreach (var anchor in candidates.Where(x => x.OccurredAt >= trigger.OccurredAt).OrderBy(x => x.OccurredAt))
        {
            var match = DetectionEngine.Evaluate(rule, anchor, candidates);
            if (match == null) continue;
            var overlap = await db.Alerts.AnyAsync(a => a.RuleId == rule.Id && a.RuleVersion == rule.Version &&
                a.Device == trigger.Device && a.Account == trigger.Account && a.Synthetic == trigger.Synthetic &&
                a.ScenarioRunId == trigger.ScenarioRunId && a.WindowEnd >= match.Start && a.WindowStart <= match.End, ct);
            overlap |= db.Alerts.Local.Any(a => a.RuleId == rule.Id && a.RuleVersion == rule.Version &&
                a.Device == trigger.Device && a.Account == trigger.Account && a.ScenarioRunId == trigger.ScenarioRunId &&
                a.WindowEnd >= match.Start && a.WindowStart <= match.End);
            if (overlap) continue;
            var alert = new Alert
            {
                RuleId = rule.Id, RuleVersion = rule.Version, Title = rule.Name, Device = trigger.Device,
                Account = trigger.Account, Severity = rule.Severity, Synthetic = trigger.Synthetic,
                ScenarioRunId = trigger.ScenarioRunId, WindowStart = match.Start, WindowEnd = match.End,
                DeduplicationKey = $"{rule.Id}:{rule.Version}:{anchor.Id}"
            };
            db.Alerts.Add(alert);
            foreach (var eventId in match.Evidence) db.AlertEvidence.Add(new() { AlertId = alert.Id, EventId = eventId });
            db.Record("detection-worker", "alert.created", alert.Id, $"rule={rule.Id}; evidence={match.Evidence.Count}");
            alerts++;
        }
        var asset = await db.Assets.SingleOrDefaultAsync(x => x.Name == trigger.Device, ct);
        if (asset != null && (asset.LastSeenAt == null || asset.LastSeenAt < trigger.OccurredAt)) asset.LastSeenAt = trigger.OccurredAt;
        db.Processed.Add(new() { EventId = id });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        Telemetry.ProcessingDelay.Record(Math.Max(0, (DateTimeOffset.UtcNow - trigger.CreatedAt).TotalSeconds));
        Telemetry.Detected.Add(alerts); Telemetry.DetectionMs.Record(watch.Elapsed.TotalMilliseconds);
        try
        {
            await redis.GetDatabase().StringSetAsync($"velqora:last-seen:{trigger.Device}", trigger.OccurredAt.ToString("O"), TimeSpan.FromDays(1));
        }
        catch (RedisException ex) { logger.LogWarning(ex, "Redis cache update failed; durable detection succeeded"); }
    }

    public async Task DetectTelemetryLossAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73002)", ct);
        var rules = await db.Rules.Where(x => x.Enabled && x.Kind == RuleKind.TelemetryLoss).ToListAsync(ct);
        var runs = await db.ScenarioRuns.Where(x => x.Scenario == "telemetry-loss" &&
            x.Status == RunStatus.Completed && x.CreatedAt > DateTimeOffset.UtcNow.AddDays(-1)).ToListAsync(ct);
        foreach (var rule in rules)
        foreach (var run in runs.Where(x => x.CreatedAt.AddSeconds(rule.WindowSeconds) <= DateTimeOffset.UtcNow))
        {
            var key = $"telemetry:{rule.Id}:{rule.Version}:{run.Id}";
            if (await db.Alerts.AnyAsync(x => x.DeduplicationKey == key, ct)) continue;
            var heartbeat = await db.Events.FirstOrDefaultAsync(x => x.ScenarioRunId == run.Id, ct);
            if (heartbeat == null) continue;
            var alert = new Alert { RuleId = rule.Id, RuleVersion = rule.Version, Title = rule.Name,
                Device = run.Device, Account = run.Account, Severity = rule.Severity, Synthetic = true,
                ScenarioRunId = run.Id, DeduplicationKey = key, WindowStart = heartbeat.OccurredAt,
                WindowEnd = heartbeat.OccurredAt.AddSeconds(rule.WindowSeconds) };
            db.Alerts.Add(alert); db.AlertEvidence.Add(new() { AlertId = alert.Id, EventId = heartbeat.Id });
            db.Record("detection-worker", "alert.telemetry-loss", alert.Id);
            Telemetry.Detected.Add(1);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
