using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Velqora.Core;
using Velqora.Infrastructure;
namespace Velqora.Api;
public static class EventEndpoints
{
    public static void MapEvents(this WebApplication app)
    {
        app.MapPost("/api/ingest/events", async (EventInput[] input, HttpContext ctx, IConfiguration config, IngestionService ingestion, CancellationToken ct) =>
        {
            var expected = config["IngestionApiKey"];
            var supplied = ctx.Request.Headers["X-Ingestion-Key"].ToString();
            if (string.IsNullOrEmpty(expected) || expected.Length < 32 ||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
                return Results.Unauthorized();
            var result = await ingestion.IngestAsync(input, ct: ct);
            return Results.Ok(new { result.Accepted, result.Duplicates });
        });
        var group = app.MapGroup("/api/events").RequireAuthorization();
        group.MapGet("", async (SocDb db, string? q, string? type, DateTimeOffset? from, DateTimeOffset? to, bool? synthetic, int? page, int? size, CancellationToken ct) =>
        {
            var query = Filter(db, q, type, from, to, synthetic);
            return Results.Ok(await ApiHelpers.PageAsync(query.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id), page, size, ct));
        });
        group.MapGet("/{id:guid}", async (Guid id, SocDb db, CancellationToken ct) =>
            Results.Ok(ApiHelpers.Required(await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct))));
        group.MapGet("/export", async (SocDb db, HttpContext ctx, string? q, string? type, DateTimeOffset? from, DateTimeOffset? to, bool? synthetic, CancellationToken ct) =>
        {
            var query = Filter(db, q, type, from, to, synthetic);
            if (await query.CountAsync(ct) > 50000) throw new DomainException("Narrow the date range to export at most 50,000 events.");
            var events = await query.OrderBy(x => x.OccurredAt).ToListAsync(ct);
            var rows = new List<string> { "id,occurredAt,source,type,device,account,ipAddress,synthetic" };
            rows.AddRange(events.Select(e => string.Join(",", new[] { e.Id.ToString(), e.OccurredAt.ToString("O"), e.Source, e.Type, e.Device, e.Account, e.IpAddress, e.Synthetic.ToString() }.Select(Guard.Csv))));
            db.Record(ctx.User.Actor(), "events.exported", "events", $"count={events.Count}");
            await db.SaveChangesAsync(ct);
            return Results.File(Encoding.UTF8.GetBytes(string.Join("\r\n", rows)), "text/csv", "velqora-events.csv");
        });
    }
    private static IQueryable<SecurityEvent> Filter(SocDb db, string? q, string? type, DateTimeOffset? from, DateTimeOffset? to, bool? synthetic)
    {
        var query = db.Events.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = Guard.Text(q, "Search", 120).ToLowerInvariant();
            query = query.Where(x => x.Device.ToLower().Contains(q) || x.Account.ToLower().Contains(q) || x.Source.ToLower().Contains(q));
        }
        if (!string.IsNullOrEmpty(type)) query = query.Where(x => x.Type == type);
        if (from != null) query = query.Where(x => x.OccurredAt >= from.Value.ToUniversalTime());
        if (to != null) query = query.Where(x => x.OccurredAt <= to.Value.ToUniversalTime());
        if (synthetic != null) query = query.Where(x => x.Synthetic == synthetic);
        return query;
    }
}
