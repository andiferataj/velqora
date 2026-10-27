using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Velqora.Core;
using Velqora.Infrastructure;
namespace Velqora.Api;
public static class OperationEndpoints
{
    private static readonly string[] Actions = ["isolate-device", "lock-account", "notify", "assign-approver", "escalate-incident"];
    public static void MapOperations(this WebApplication app)
    {
        var rules = app.MapGroup("/api/rules").RequireAuthorization();
        rules.MapGet("", async (SocDb db, CancellationToken ct) => Results.Ok(await db.Rules.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)));
        rules.MapPost("", async (RuleInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            Guard.Rule(input);
            var rule = new DetectionRule(); Apply(rule, input); db.Rules.Add(rule);
            db.Record(ctx.User.Actor(), "rule.created", rule.Id, JsonSerializer.Serialize(input));
            await db.SaveChangesAsync(ct); return Results.Created($"/api/rules/{rule.Id}", rule);
        }).RequireAuthorization("Admin");
        rules.MapPut("/{id:guid}", async (Guid id, RuleInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            Guard.Rule(input);
            var rule = ApiHelpers.Required(await db.Rules.FindAsync([id], ct));
            ApiHelpers.Version(rule, input.Version); Apply(rule, input);
            db.Record(ctx.User.Actor(), "rule.updated", id, JsonSerializer.Serialize(input));
            await db.SaveChangesAsync(ct); return Results.Ok(rule);
        }).RequireAuthorization("Admin");
        app.MapGet("/api/assets", async (SocDb db, string? q, int? page, int? size, CancellationToken ct) =>
        {
            var query = db.Assets.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q) || x.Owner.Contains(q));
            return Results.Ok(await ApiHelpers.PageAsync(query.OrderBy(x => x.Name), page, size, ct));
        }).RequireAuthorization();
        app.MapPost("/api/assets", async (AssetInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            var name = Guard.Text(input.Name, "Name", 120);
            if (await db.Assets.AnyAsync(x => x.Name == name, ct)) throw new DomainException("Asset name already exists.", 409);
            var asset = new Asset { Name = name, Kind = Guard.Text(input.Kind, "Kind", 80), Owner = Guard.Text(input.Owner, "Owner", 120),
                Account = Guard.Text(input.Account, "Account", 120, false), IpAddress = Guard.Text(input.IpAddress, "IP", 100, false) };
            db.Assets.Add(asset); db.Record(ctx.User.Actor(), "asset.created", asset.Id);
            await db.SaveChangesAsync(ct); return Results.Created($"/api/assets/{asset.Id}", asset);
        }).RequireAuthorization("Admin");
        app.MapGet("/api/assets/{id:guid}", async (Guid id, SocDb db, CancellationToken ct) =>
        {
            var asset = ApiHelpers.Required(await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct));
            var alerts = await db.Alerts.AsNoTracking().Where(x => x.Device == asset.Name).OrderByDescending(x => x.DetectedAt).Take(50).ToListAsync(ct);
            var events = await db.Events.AsNoTracking().Where(x => x.Device == asset.Name).OrderByDescending(x => x.OccurredAt).Take(100).ToListAsync(ct);
            return Results.Ok(new { asset, alerts, events, risk = alerts.Count(x => x.Status != AlertStatus.Resolved && x.Status != AlertStatus.FalsePositive) });
        }).RequireAuthorization();
        app.MapGet("/api/playbooks", async (SocDb db, CancellationToken ct) => Results.Ok(await db.Playbooks.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct))).RequireAuthorization();
        app.MapPost("/api/playbooks", async (PlaybookInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            if (!Actions.Contains(input.Action) || input.Trigger is not ("Manual" or "IncidentCreated")) throw new DomainException("Unsupported action or trigger.");
            Guard.EnumValue(input.MinimumSeverity);
            var book = new Playbook { Name = Guard.Text(input.Name, "Name", 120), Description = Guard.Text(input.Description, "Description", 1000),
                Action = input.Action, Enabled = input.Enabled, Trigger = input.Trigger, MinimumSeverity = input.MinimumSeverity };
            db.Playbooks.Add(book); db.Record(ctx.User.Actor(), "playbook.created", book.Id);
            await db.SaveChangesAsync(ct); return Results.Ok(book);
        }).RequireAuthorization("Admin");
        app.MapPut("/api/playbooks/{id:guid}", async (Guid id, PlaybookInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            if (!Actions.Contains(input.Action) || input.Trigger is not ("Manual" or "IncidentCreated")) throw new DomainException("Unsupported action or trigger.");
            Guard.EnumValue(input.MinimumSeverity);
            var book = ApiHelpers.Required(await db.Playbooks.FindAsync([id], ct)); ApiHelpers.Version(book, input.Version);
            book.Name = Guard.Text(input.Name, "Name", 120); book.Description = Guard.Text(input.Description, "Description", 1000);
            book.Action = input.Action; book.Enabled = input.Enabled; book.Trigger = input.Trigger; book.MinimumSeverity = input.MinimumSeverity;
            db.Record(ctx.User.Actor(), "playbook.updated", id, JsonSerializer.Serialize(input)); await db.SaveChangesAsync(ct); return Results.Ok(book);
        }).RequireAuthorization("Admin");
        app.MapGet("/api/playbook-runs", async (SocDb db, int? page, int? size, CancellationToken ct) =>
            Results.Ok(await ApiHelpers.PageAsync(db.PlaybookRuns.AsNoTracking().OrderByDescending(x => x.CreatedAt), page, size, ct))).RequireAuthorization();
        app.MapPost("/api/playbook-runs", async (PlaybookRequest input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            var book = ApiHelpers.Required(await db.Playbooks.FindAsync([input.PlaybookId], ct));
            var asset = ApiHelpers.Required(await db.Assets.FindAsync([input.AssetId], ct));
            ApiHelpers.Required(await db.Incidents.FindAsync([input.IncidentId], ct));
            if (!book.Enabled || !asset.Synthetic) throw new DomainException("Select an enabled playbook and a synthetic asset.");
            var linkedDevice = await (from a in db.Alerts join l in db.IncidentAlerts on a.Id equals l.AlertId where l.IncidentId == input.IncidentId && a.Device == asset.Name select a.Id).AnyAsync(ct);
            if (!linkedDevice) throw new DomainException("The asset must be linked to this incident through alert evidence.");
            var run = new PlaybookRun { PlaybookId = input.PlaybookId, AssetId = input.AssetId, IncidentId = input.IncidentId, RequestedBy = ctx.User.Actor(), ActionSnapshot = book.Action, PlaybookVersion = book.Version };
            db.PlaybookRuns.Add(run);
            db.Timeline.Add(new() { IncidentId = input.IncidentId, ActorId = ctx.User.Actor(), Kind = "approval", Text = $"Requested simulated {book.Action} for {asset.Name}." });
            db.Record(ctx.User.Actor(), "playbook.requested", run.Id);
            await db.SaveChangesAsync(ct); return Results.Ok(run);
        }).RequireAuthorization("Operate");
        app.MapPost("/api/playbook-runs/{id:guid}/decision", async (Guid id, DecisionInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            var run = ApiHelpers.Required(await db.PlaybookRuns.FindAsync([id], ct)); ApiHelpers.Version(run, input.Version);
            if (run.Status != ApprovalStatus.Pending) throw new DomainException("Only pending requests can be decided.", 409);
            if (run.RequestedBy == ctx.User.Actor()) throw new DomainException("A different operator must approve or reject this request.", 403);
            run.Status = input.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected; run.ApprovedBy = ctx.User.Actor();
            db.Record(ctx.User.Actor(), input.Approve ? "playbook.approved" : "playbook.rejected", id);
            db.Timeline.Add(new() { IncidentId = run.IncidentId, ActorId = ctx.User.Actor(), Kind = "approval", Text = $"Response request {run.Status}." });
            await db.SaveChangesAsync(ct); return Results.Ok(run);
        }).RequireAuthorization("Operate");
        app.MapGet("/api/scenarios", () => Results.Ok(Scenarios.Names.Select(n => new { name = n, totalEvents = Scenarios.Count(n) }))).RequireAuthorization();
        app.MapGet("/api/scenario-runs", async (SocDb db, CancellationToken ct) =>
        {
            var runs = await db.ScenarioRuns.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
            var ids = runs.Select(x => x.Id).ToArray();
            var outcomes = await db.Alerts.Where(x => x.ScenarioRunId != null && ids.Contains(x.ScenarioRunId.Value))
                .GroupBy(x => x.ScenarioRunId).Select(g => new { runId = g.Key, alerts = g.Count() }).ToListAsync(ct);
            return Results.Ok(new { runs, outcomes });
        }).RequireAuthorization();
        app.MapPost("/api/scenario-runs", async (ScenarioInput input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            var count = Scenarios.Count(input.Scenario);
            if (await db.ScenarioRuns.CountAsync(x => x.Status == RunStatus.Running || x.Status == RunStatus.Paused, ct) >= 10)
                throw new DomainException("At most 10 active scenarios are allowed.");
            var run = new ScenarioRun { Scenario = input.Scenario, TotalEvents = count, RequestedBy = ctx.User.Actor() };
            run.Device = $"LAB-{run.Id.ToString("N")[..8]}"; run.Account = $"lab.{run.Id.ToString("N")[..8]}";
            db.Assets.Add(new() { Name = run.Device, Account = run.Account, Owner = "Simulation Lab", IpAddress = "192.0.2.42", Kind = "Lab workstation" });
            db.ScenarioRuns.Add(run); db.Record(ctx.User.Actor(), "scenario.started", run.Id, input.Scenario);
            await db.SaveChangesAsync(ct); return Results.Ok(run);
        }).RequireAuthorization("Operate");
        app.MapPost("/api/scenario-runs/{id:guid}/reset", async (Guid id, ScenarioControl input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73004)", ct);
            var previous = ApiHelpers.Required(await db.ScenarioRuns.FindAsync([id], ct));
            ApiHelpers.Version(previous, input.Version);
            if (previous.Status is RunStatus.Running or RunStatus.Paused) previous.Status = RunStatus.Cancelled;
            var run = new ScenarioRun { Scenario = previous.Scenario, TotalEvents = previous.TotalEvents, RequestedBy = ctx.User.Actor() };
            run.Device = $"LAB-{run.Id.ToString("N")[..8]}"; run.Account = $"lab.{run.Id.ToString("N")[..8]}";
            db.Assets.Add(new() { Name = run.Device, Account = run.Account, Owner = "Simulation Lab", IpAddress = "192.0.2.42", Kind = "Lab workstation" });
            db.ScenarioRuns.Add(run); db.Record(ctx.User.Actor(), "scenario.reset", previous.Id, $"new-run={run.Id}");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(run);
        }).RequireAuthorization("Operate");
        app.MapPut("/api/scenario-runs/{id:guid}", async (Guid id, ScenarioControl input, SocDb db, HttpContext ctx, CancellationToken ct) =>
        {
            var run = ApiHelpers.Required(await db.ScenarioRuns.FindAsync([id], ct)); ApiHelpers.Version(run, input.Version);
            run.Status = input.Action switch
            {
                "pause" when run.Status == RunStatus.Running => RunStatus.Paused,
                "resume" when run.Status == RunStatus.Paused => RunStatus.Running,
                "cancel" when run.Status is RunStatus.Running or RunStatus.Paused => RunStatus.Cancelled,
                _ => throw new DomainException("Invalid scenario transition.")
            };
            db.Record(ctx.User.Actor(), "scenario." + input.Action, id);
            await db.SaveChangesAsync(ct); return Results.Ok(run);
        }).RequireAuthorization("Operate");
    }
    private static void Apply(DetectionRule rule, RuleInput input)
    {
        rule.Name = input.Name.Trim(); rule.Description = input.Description.Trim(); rule.Kind = input.Kind;
        rule.EventType = input.EventType.Trim(); rule.Threshold = input.Threshold; rule.WindowSeconds = input.WindowSeconds;
        rule.Severity = input.Severity; rule.Enabled = input.Enabled;
    }
}
