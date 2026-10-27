using Microsoft.EntityFrameworkCore;
using Velqora.Infrastructure;
using Velqora.Worker;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();
builder.Services.AddSoc(builder.Configuration, "velqora-worker");
builder.Services.AddHostedService<PipelineWorker>();
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (SocDb db, CancellationToken ct) =>
{
    var h = await db.Heartbeats.AsNoTracking().SingleOrDefaultAsync(x => x.Service == "pipeline-worker", ct);
    return h != null && h.LastSeenAt > DateTimeOffset.UtcNow.AddSeconds(-20) && h.State == "Healthy"
        ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
});
await app.RunAsync();
