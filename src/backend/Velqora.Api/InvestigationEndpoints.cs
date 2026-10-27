using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Velqora.Core;
using Velqora.Infrastructure;
namespace Velqora.Api;
public static class InvestigationEndpoints
{
    public static void MapInvestigations(this WebApplication app)
    {
        var alerts = app.MapGroup("/api/alerts").RequireAuthorization();
        alerts.MapGet("", async (SocDb db, string? q, AlertStatus? status, Severity? severity, int? page, int? size, CancellationToken ct) =>
        {
            var query = db.Alerts.AsNoTracking();
            if (status != null) query = query.Where(x => x.Status == status);
            if (severity != null) query = query.Where(x => x.Severity == severity);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.Contains(q) || x.Device.Contains(q) || x.Account.Contains(q));
            return Results.Ok(await ApiHelpers.PageAsync(query.OrderByDescending(x => x.DetectedAt).ThenBy(x => x.Id), page, size, ct));
        });
        alerts.MapGet("/{id:guid}", async (Guid id, SocDb db, CancellationToken ct) =>
        {
            var alert = ApiHelpers.Required(await db.Alerts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct));
            var evidence = await (from e in db.Events join a in db.AlertEvidence on e.Id equals a.EventId where a.AlertId == id orderby e.OccurredAt select e).ToListAsync(ct);
            return Results.Ok(new { alert, evidence, rule = await db.Rules.AsNoTracking().SingleAsync(x => x.Id == alert.RuleId, ct) });
        });
        alerts.MapPut("/{id:guid}", async (Guid id, AlertUpdate input, SocDb db, UserManager<SocUser> users, HttpContext ctx, CancellationToken ct) =>
        {
            var alert = ApiHelpers.Required(await db.Alerts.SingleOrDefaultAsync(x => x.Id == id, ct));
            ApiHelpers.Version(alert, input.Version); Guard.EnumValue(input.Status); await ApiHelpers.Owner(users, input.OwnerId);
            alert.Status = input.Status; alert.OwnerId = input.OwnerId;
            alert.Feedback = Guard.Text(input.Feedback, "Feedback", 1000, false);
            db.Record(ctx.User.Actor(), "alert.updated", id, JsonSerializer.Serialize(input));
            await db.SaveChangesAsync(ct); return Results.Ok(alert);
        }).RequireAuthorization("Operate");

        var incidents = app.MapGroup("/api/incidents").RequireAuthorization();
        incidents.MapGet("", async (SocDb db, string? q, IncidentStatus? status, int? page, int? size, CancellationToken ct) =>
        {
            var query = db.Incidents.AsNoTracking();
            if (status != null) query = query.Where(x => x.Status == status);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.Contains(q));
            return Results.Ok(await ApiHelpers.PageAsync(query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id), page, size, ct));
        });
        incidents.MapPost("", async (IncidentInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            Guard.EnumValue(input.Severity);
            var ids = input.AlertIds?.Distinct().ToArray() ?? [];
            if (ids.Length is < 1 or > 100 || await db.Alerts.CountAsync(x => ids.Contains(x.Id), ct) != ids.Length)
                throw new DomainException("Select 1–100 existing alerts.");
            var incident = new Incident { Title = Guard.Text(input.Title, "Title", 200),
                Summary = Guard.Text(input.Summary, "Summary", 3000, false), Severity = input.Severity, OwnerId = ctx.User.Actor() };
            db.Incidents.Add(incident);
            foreach (var id in ids) db.IncidentAlerts.Add(new() { IncidentId = incident.Id, AlertId = id });
            db.Timeline.Add(new() { IncidentId = incident.Id, ActorId = ctx.User.Actor(), Kind = "created", Text = $"Case opened with {ids.Length} alert(s)." });
            db.Record(ctx.User.Actor(), "incident.created", incident.Id);
            await new OperationsService(db).QueueIncidentPlaybooksAsync(incident, ids, ct);
            await db.SaveChangesAsync(ct); return Results.Created($"/api/incidents/{incident.Id}", incident);
        }).RequireAuthorization("Operate");
        incidents.MapGet("/{id:guid}", async (Guid id, SocDb db, CancellationToken ct) =>
        {
            var incident = ApiHelpers.Required(await db.Incidents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct));
            var linked = await (from a in db.Alerts join link in db.IncidentAlerts on a.Id equals link.AlertId where link.IncidentId == id select a).ToListAsync(ct);
            var timeline = await db.Timeline.AsNoTracking().Where(x => x.IncidentId == id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
            return Results.Ok(new { incident, alerts = linked, timeline });
        });
        incidents.MapPut("/{id:guid}", async (Guid id, IncidentUpdate input, SocDb db, UserManager<SocUser> users, HttpContext ctx, CancellationToken ct) =>
        {
            var incident = ApiHelpers.Required(await db.Incidents.SingleOrDefaultAsync(x => x.Id == id, ct));
            ApiHelpers.Version(incident, input.Version); Guard.EnumValue(input.Status); Guard.EnumValue(input.Severity);
            await ApiHelpers.Owner(users, input.OwnerId);
            if (!DetectionEngine.CanTransition(incident.Status, input.Status)) throw new DomainException("Invalid incident status transition.");
            var oldStatus = incident.Status;
            incident.Status = input.Status; incident.Severity = input.Severity; incident.OwnerId = input.OwnerId;
            incident.Summary = Guard.Text(input.Summary, "Summary", 3000, false);
            incident.ResolvedAt = input.Status is IncidentStatus.Resolved or IncidentStatus.Closed ? incident.ResolvedAt ?? DateTimeOffset.UtcNow : null;
            db.Timeline.Add(new() { IncidentId = id, ActorId = ctx.User.Actor(), Kind = "updated", Text = $"Status: {oldStatus} → {input.Status}. Severity: {input.Severity}. Owner: {input.OwnerId ?? "unassigned"}." });
            db.Record(ctx.User.Actor(), "incident.updated", id, JsonSerializer.Serialize(input));
            await db.SaveChangesAsync(ct); return Results.Ok(incident);
        }).RequireAuthorization("Operate");
        incidents.MapPost("/{id:guid}/comments", async (Guid id, CommentInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            ApiHelpers.Required(await db.Incidents.FindAsync([id], ct));
            var comment = new TimelineEntry { IncidentId = id, ActorId = ctx.User.Actor(), Kind = "comment", Text = Guard.Text(input.Text, "Comment", 3000) };
            db.Timeline.Add(comment); db.Record(ctx.User.Actor(), "incident.comment", id);
            await db.SaveChangesAsync(ct); return Results.Ok(comment);
        }).RequireAuthorization("Operate");
        incidents.MapPost("/{id:guid}/alerts", async (Guid id, EvidenceInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            ApiHelpers.Required(await db.Incidents.FindAsync([id], ct)); ApiHelpers.Required(await db.Alerts.FindAsync([input.AlertId], ct));
            if (await db.IncidentAlerts.AnyAsync(x => x.IncidentId == id && x.AlertId == input.AlertId, ct)) return Results.NoContent();
            db.IncidentAlerts.Add(new() { IncidentId = id, AlertId = input.AlertId });
            db.Timeline.Add(new() { IncidentId = id, ActorId = ctx.User.Actor(), Kind = "evidence", Text = $"Linked alert {input.AlertId}." });
            db.Record(ctx.User.Actor(), "incident.evidence-linked", id, input.AlertId.ToString());
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("Operate");
        incidents.MapGet("/{id:guid}/report", async (Guid id, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            var incident = ApiHelpers.Required(await db.Incidents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct));
            var linked = await (from a in db.Alerts join l in db.IncidentAlerts on a.Id equals l.AlertId where l.IncidentId == id select a).ToListAsync(ct);
            var timeline = await db.Timeline.AsNoTracking().Where(x => x.IncidentId == id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
            var evidence = await (from e in db.Events join ae in db.AlertEvidence on e.Id equals ae.EventId join ia in db.IncidentAlerts on ae.AlertId equals ia.AlertId where ia.IncidentId == id select e).Distinct().ToListAsync(ct);
            db.Record(ctx.User.Actor(), "incident.exported", id); await db.SaveChangesAsync(ct);
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(new { exportedAt = DateTimeOffset.UtcNow, incident, alerts = linked, evidence, timeline },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }),
                "application/json", $"incident-{id}.json");
        });
    }
}
