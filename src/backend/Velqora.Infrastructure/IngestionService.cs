using Microsoft.EntityFrameworkCore;
using Velqora.Core;
namespace Velqora.Infrastructure;

public sealed class IngestionService(SocDb db)
{
    public async Task<(int Accepted, int Duplicates)> IngestAsync(EventInput[] input, Guid? scenario = null, CancellationToken ct = default)
    {
        if (input.Length is < 1 or > 1000) throw new DomainException("A batch must contain 1–1000 events.");
        var events = input.Select(e => Guard.Event(e, DateTimeOffset.UtcNow)).ToArray();
        var conflicting = events.GroupBy(e => e.Id).Any(g => g.Select(e => e.PayloadHash).Distinct().Count() > 1);
        if (conflicting) throw new DomainException("An event ID was reused with different data.", 409);
        events = events.DistinctBy(e => e.Id).ToArray();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73001)", ct);
        var ids = events.Select(e => e.Id).ToArray();
        var existing = await db.Events.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        foreach (var e in events)
        {
            if (existing.TryGetValue(e.Id, out var old))
            {
                if (old.PayloadHash != e.PayloadHash || old.ScenarioRunId != scenario)
                    throw new DomainException("An event ID was reused with different data.", 409);
                continue;
            }
            e.ScenarioRunId = scenario;
            db.Events.Add(e);
            db.Outbox.Add(new OutboxMessage { EventId = e.Id, TraceParent = System.Diagnostics.Activity.Current?.Id });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var accepted = events.Length - existing.Count;
        Telemetry.Ingested.Add(accepted);
        return (accepted, input.Length - accepted);
    }
}
