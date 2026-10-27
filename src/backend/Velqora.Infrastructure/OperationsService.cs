using Microsoft.EntityFrameworkCore;
using Velqora.Core;
namespace Velqora.Infrastructure;

public sealed class OperationsService(SocDb db)
{
    public async Task QueueIncidentPlaybooksAsync(Incident incident, Guid[] alertIds, CancellationToken ct)
    {
        var books = await db.Playbooks.Where(x => x.Enabled && x.Trigger == "IncidentCreated" &&
            (int)x.MinimumSeverity <= (int)incident.Severity).ToListAsync(ct);
        if (books.Count == 0) return;
        var devices = await db.Alerts.Where(x => alertIds.Contains(x.Id)).Select(x => x.Device).Distinct().ToListAsync(ct);
        var assets = await db.Assets.Where(x => x.Synthetic && devices.Contains(x.Name)).Take(100).ToListAsync(ct);
        foreach (var book in books)
        foreach (var asset in assets)
        {
            var run = new PlaybookRun { PlaybookId = book.Id, IncidentId = incident.Id, AssetId = asset.Id,
                RequestedBy = "system:incident-created", ActionSnapshot = book.Action, PlaybookVersion = book.Version };
            db.PlaybookRuns.Add(run);
            db.Timeline.Add(new() { IncidentId = incident.Id, ActorId = "automation-trigger", Kind = "approval",
                Text = $"Automatic proposal: {book.Action} on {asset.Name}. Operator approval required." });
            db.Record("automation-trigger", "playbook.proposed", run.Id, $"playbook-version={book.Version}");
        }
    }

    public async Task AutomateAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73003)", ct);
        var runs = await db.PlaybookRuns.Where(x => x.Status == ApprovalStatus.Approved && x.NextAttemptAt <= DateTimeOffset.UtcNow).Take(20).ToListAsync(ct);
        foreach (var run in runs)
        {
            run.Attempts++;
            try
            {
                var playbook = await db.Playbooks.SingleAsync(x => x.Id == run.PlaybookId, ct);
                var asset = await db.Assets.SingleAsync(x => x.Id == run.AssetId, ct);
                if (!playbook.Enabled || !asset.Synthetic) throw new DomainException("Only enabled playbooks against synthetic assets can execute.");
                if (string.IsNullOrEmpty(run.ApprovedBy) || run.ApprovedBy == run.RequestedBy) throw new DomainException("Independent approval is required.");
                var incident = await db.Incidents.SingleAsync(x => x.Id == run.IncidentId, ct);
                switch (run.ActionSnapshot)
                {
                    case "isolate-device": asset.Isolated = true; break;
                    case "lock-account": asset.AccountLocked = true; break;
                    case "notify": break;
                    case "assign-approver": incident.OwnerId = run.ApprovedBy; break;
                    case "escalate-incident": incident.Severity = Severity.Critical; break;
                    default: throw new DomainException("Unsupported playbook action.");
                }
                run.Status = ApprovalStatus.Executed;
                run.Result = $"SIMULATED: {run.ActionSnapshot} completed for {asset.Name}. No external system was changed.";
                run.ExecutedAt = DateTimeOffset.UtcNow;
                db.Timeline.Add(new() { IncidentId = run.IncidentId, ActorId = "automation-worker", Kind = "response", Text = run.Result });
                db.Record("automation-worker", "playbook.executed", run.Id, run.Result);
            }
            catch (DomainException ex)
            {
                run.Result = ex.Message; run.Status = ApprovalStatus.Failed;
                db.Record("automation-worker", "playbook.failed", run.Id, ex.Message);
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task SimulateAsync(CancellationToken ct)
    {
        var run = await db.ScenarioRuns.Where(x => x.Status == RunStatus.Running).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (run == null) return;
        // A transaction-level lease prevents duplicate advancement by multiple worker instances.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73004)", ct);
        await db.Entry(run).ReloadAsync(ct);
        if (run.Status != RunStatus.Running) return;
        var index = run.Cursor;
        if (index < run.TotalEvents)
        {
            var e = Guard.Event(Scenarios.Event(run, index), DateTimeOffset.UtcNow);
            e.ScenarioRunId = run.Id;
            if (!await db.Events.AnyAsync(x => x.Id == e.Id, ct))
            {
                db.Events.Add(e); db.Outbox.Add(new() { EventId = e.Id, TraceParent = System.Diagnostics.Activity.Current?.Id });
                Telemetry.Ingested.Add(1);
            }
            run.Cursor++;
        }
        if (run.Cursor >= run.TotalEvents)
        {
            run.Status = RunStatus.Completed; run.FinishedAt = DateTimeOffset.UtcNow;
            db.Record("simulation-worker", "scenario.completed", run.Id);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
