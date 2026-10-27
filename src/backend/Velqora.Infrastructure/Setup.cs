using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using Velqora.Core;

namespace Velqora.Infrastructure;

public static class Telemetry
{
    public const string Name = "Velqora";
    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);
    public static long PendingOutbox;
    public static long PendingQueue;
    public static long DeadLetters;
    public static readonly ObservableGauge<long> OutboxGauge = Meter.CreateObservableGauge("velqora.outbox.pending", () => Interlocked.Read(ref PendingOutbox));
    public static readonly ObservableGauge<long> QueueGauge = Meter.CreateObservableGauge("velqora.queue.pending", () => Interlocked.Read(ref PendingQueue));
    public static readonly ObservableGauge<long> DeadGauge = Meter.CreateObservableGauge("velqora.queue.dead_letters", () => Interlocked.Read(ref DeadLetters));
    public static readonly Histogram<double> ProcessingDelay = Meter.CreateHistogram<double>("velqora.event.processing.delay", "s");
    public static readonly Counter<long> Ingested = Meter.CreateCounter<long>("velqora.events.ingested");
    public static readonly Counter<long> Detected = Meter.CreateCounter<long>("velqora.alerts.created");
    public static readonly Counter<long> Failures = Meter.CreateCounter<long>("velqora.processing.failures");
    public static readonly Histogram<double> DetectionMs = Meter.CreateHistogram<double>("velqora.detection.duration", "ms");
}
public static class Setup
{
    public static IServiceCollection AddSoc(this IServiceCollection services, IConfiguration config, string service)
    {
        var cs = config.GetConnectionString("Postgres") ?? throw new InvalidOperationException("ConnectionStrings__Postgres is required.");
        services.AddDbContext<SocDb>(o => o.UseNpgsql(cs));
        services.AddIdentityCore<SocUser>(o =>
        {
            o.Password.RequiredLength = 12; o.Password.RequireNonAlphanumeric = true;
            o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            o.User.RequireUniqueEmail = true;
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<SocDb>().AddSignInManager();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config["Redis"] ?? throw new InvalidOperationException("Redis is required.")));
        services.AddSingleton<MessageBus>();
        services.AddScoped<IngestionService>();
        services.AddScoped<DetectionService>();
        services.AddScoped<OperationsService>();
        services.AddOpenTelemetry().ConfigureResource(r => r.AddService(service))
            .WithTracing(t => t.AddSource(Telemetry.Name).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter())
            .WithMetrics(m => m.AddMeter(Telemetry.Name).AddAspNetCoreInstrumentation().AddOtlpExporter());
        return services;
    }

    public static async Task SeedAsync(IServiceProvider provider, IConfiguration config, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SocDb>();
        await db.Database.MigrateAsync(ct);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Analyst", "ReadOnly" })
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new(role)));
        var password = config["DemoPassword"] ?? throw new InvalidOperationException("DemoPassword is required for initial accounts.");
        if (password.Length < 16 || password.StartsWith("CHANGE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generate local credentials with infrastructure/scripts/bootstrap.ps1.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SocUser>>();
        foreach (var (email, role) in new[] { ("admin@velqora.local", "Admin"), ("analyst@velqora.local", "Analyst"), ("viewer@velqora.local", "ReadOnly") })
        {
            var user = await users.FindByEmailAsync(email);
            if (user == null)
            {
                user = new SocUser { UserName = email, Email = email, EmailConfirmed = true };
                Check(await users.CreateAsync(user, password)); Check(await users.AddToRoleAsync(user, role));
            }
        }
        if (!await db.Rules.AnyAsync(ct))
            db.Rules.AddRange(
                new DetectionRule { Name = "Repeated login failures", Description = "10 failed logins from one device and account within 5 minutes.", Threshold = 10 },
                new DetectionRule { Name = "Success after login abuse", Description = "Successful login after 10 failures.", Kind = RuleKind.FailureThenSuccess, Severity = Severity.Critical },
                new DetectionRule { Name = "Privileged account change", Description = "Privilege modification requires investigation.", Kind = RuleKind.PrivilegeMisuse, EventType = "privilege.change", Threshold = 1 },
                new DetectionRule { Name = "Network event spike", Description = "100 network events in 60 seconds.", EventType = "network.connection", Threshold = 100, WindowSeconds = 60, Severity = Severity.Medium },
                new DetectionRule { Name = "Telemetry stopped", Description = "A monitored synthetic telemetry stream has not checked in for 30 seconds.", Kind = RuleKind.TelemetryLoss, EventType = "heartbeat", Threshold = 1, WindowSeconds = 30, Severity = Severity.Medium });
        if (!await db.Playbooks.AnyAsync(ct))
            db.Playbooks.AddRange(
                new Playbook { Name = "Contain workstation", Description = "Approval-gated simulated device isolation.", Action = "isolate-device" },
                new Playbook { Name = "Lock account", Description = "Approval-gated simulated account lock.", Action = "lock-account" },
                new Playbook { Name = "Notify incident owner", Description = "Record a local notification in the case timeline.", Action = "notify" },
                new Playbook { Name = "Assign approving analyst", Description = "Assign the incident to the operator who approves this response.", Action = "assign-approver" },
                new Playbook { Name = "Escalate critical case", Description = "Raise incident severity after a second operator approves.", Action = "escalate-incident" });
        if (!await db.Assets.AnyAsync(ct))
            db.Assets.AddRange(
                new Asset { Name = "WS-PRISHTINA-01", Owner = "Security Lab", Account = "a.ferataj", IpAddress = "192.0.2.10" },
                new Asset { Name = "SRV-IDENTITY-01", Kind = "Server", Owner = "Platform", Account = "svc.identity", IpAddress = "192.0.2.20" },
                new Asset { Name = "EDGE-GATEWAY-01", Kind = "Network", Owner = "Infrastructure", Account = "svc.edge", IpAddress = "192.0.2.30" });
        await db.SaveChangesAsync(ct);
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
