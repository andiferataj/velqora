using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Velqora.Api;
using Velqora.Core;
using Velqora.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 2 * 1024 * 1024);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSoc(builder.Configuration, "velqora-api");
builder.Services.AddDataProtection().SetApplicationName("Velqora").PersistKeysToFileSystem(
    new DirectoryInfo(builder.Configuration["DataProtectionPath"] ?? Path.Combine(builder.Environment.ContentRootPath, ".keys")));
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme, o =>
{
    o.Cookie.Name = "velqora.session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Configuration.GetValue<bool>("RequireHttps") ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false;
    o.Events.OnValidatePrincipal = Microsoft.AspNetCore.Identity.SecurityStampValidator.ValidatePrincipalAsync;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Operate", p => p.RequireRole("Admin", "Analyst"));
    o.AddPolicy("Admin", p => p.RequireRole("Admin"));
});
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-XSRF-TOKEN"; o.Cookie.SameSite = SameSiteMode.Strict; });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c => RateLimitPartition.GetFixedWindowLimiter(
        c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 600, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 16384);
builder.Services.AddHostedService<LivePublisher>();
var app = builder.Build();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    try { await next(); }
    catch (Exception ex)
    {
        if (ctx.Response.HasStarted) throw;
        var status = ex switch
        {
            DomainException d => d.Status, DbUpdateConcurrencyException => 409,
            DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" } } => 409,
            AntiforgeryValidationException => 400, BadHttpRequestException => 400,
            _ => 500
        };
        if (status == 500) app.Logger.LogError(ex, "Request failed {TraceId}", ctx.TraceIdentifier);
        await Results.Problem(statusCode: status, title: status == 500 ? "The request could not be completed." :
            status == 409 ? "The resource changed. Reload it and try again." : ex.Message,
            extensions: new Dictionary<string, object?> { ["traceId"] = ctx.TraceIdentifier }).ExecuteAsync(ctx);
    }
});
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) &&
        !HttpMethods.IsHead(ctx.Request.Method) && ctx.Request.Path != "/api/ingest/events")
        await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx);
    await next();
});
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (SocDb db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapAuth(); app.MapEvents(); app.MapInvestigations(); app.MapOperations(); app.MapReporting();
app.MapHub<SocHub>("/hubs/soc").RequireAuthorization();
await Setup.SeedAsync(app.Services, app.Configuration);
if (builder.Configuration.GetValue<bool>("MigrateOnly")) return;
await app.RunAsync();

public partial class Program { }
