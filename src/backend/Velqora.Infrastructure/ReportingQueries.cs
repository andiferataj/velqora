using Microsoft.EntityFrameworkCore;
namespace Velqora.Infrastructure;

public sealed record DailyActivity(DateTimeOffset Day, int Count);
public static class ReportingQueries
{
    public static Task<List<DailyActivity>> DailyActivityAsync(SocDb db, DateTimeOffset since, CancellationToken ct) =>
        // Keep the UTC day as timestamptz. Npgsql cannot shape DateTimeOffset.UtcDateTime.Date
        // in a grouped projection correctly; the explicit SQL also makes the UTC boundary unambiguous.
        db.Database.SqlQuery<DailyActivity>($"""
            SELECT date_trunc('day', "OccurredAt", 'UTC') AS "Day", count(*)::int AS "Count"
            FROM "SecurityEvents"
            WHERE "OccurredAt" >= {since}
            GROUP BY 1
            ORDER BY 1
            """).ToListAsync(ct);
}
