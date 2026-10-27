using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Velqora.Core;

namespace Velqora.Infrastructure;

public sealed class SocUser : IdentityUser { }
public sealed class SocDb(DbContextOptions<SocDb> options) : IdentityDbContext<SocUser>(options)
{
    public DbSet<SecurityEvent> Events => Set<SecurityEvent>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> Processed => Set<ProcessedMessage>();
    public DbSet<DetectionRule> Rules => Set<DetectionRule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertEvidence> AlertEvidence => Set<AlertEvidence>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentAlert> IncidentAlerts => Set<IncidentAlert>();
    public DbSet<TimelineEntry> Timeline => Set<TimelineEntry>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Playbook> Playbooks => Set<Playbook>();
    public DbSet<PlaybookRun> PlaybookRuns => Set<PlaybookRun>();
    public DbSet<ScenarioRun> ScenarioRuns => Set<ScenarioRun>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    public DbSet<ServiceHeartbeat> Heartbeats => Set<ServiceHeartbeat>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<SecurityEvent>().ToTable("SecurityEvents");
        b.Entity<OutboxMessage>().ToTable("OutboxMessages");
        b.Entity<ProcessedMessage>().ToTable("ProcessedMessages");
        b.Entity<DetectionRule>().ToTable("DetectionRules");
        b.Entity<Alert>().ToTable("Alerts");
        b.Entity<AlertEvidence>().ToTable("AlertEvidence");
        b.Entity<Incident>().ToTable("Incidents");
        b.Entity<IncidentAlert>().ToTable("IncidentAlerts");
        b.Entity<TimelineEntry>().ToTable("IncidentTimeline");
        b.Entity<Asset>().ToTable("Assets");
        b.Entity<Playbook>().ToTable("Playbooks");
        b.Entity<PlaybookRun>().ToTable("PlaybookRuns");
        b.Entity<ScenarioRun>().ToTable("ScenarioRuns");
        b.Entity<AuditEvent>().ToTable("AuditEvents");
        b.Entity<ServiceHeartbeat>().ToTable("ServiceHeartbeats");
        b.Entity<SecurityEvent>().HasIndex(x => new { x.Device, x.Account, x.OccurredAt });
        b.Entity<SecurityEvent>().HasIndex(x => x.OccurredAt);
        b.Entity<SecurityEvent>().HasIndex(x => x.ScenarioRunId);
        b.Entity<SecurityEvent>().Property(x => x.AttributesJson).HasColumnType("jsonb");
        b.Entity<OutboxMessage>().HasIndex(x => x.EventId).IsUnique();
        b.Entity<OutboxMessage>().HasIndex(x => new { x.PublishedAt, x.NextAttemptAt });
        b.Entity<OutboxMessage>().HasOne<SecurityEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProcessedMessage>().HasIndex(x => x.EventId).IsUnique();
        b.Entity<ProcessedMessage>().HasOne<SecurityEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Alert>().HasIndex(x => x.DeduplicationKey).IsUnique();
        b.Entity<Alert>().HasIndex(x => x.DetectedAt);
        b.Entity<Alert>().HasOne<DetectionRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AlertEvidence>().HasIndex(x => new { x.AlertId, x.EventId }).IsUnique();
        b.Entity<AlertEvidence>().HasOne<Alert>().WithMany().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AlertEvidence>().HasOne<SecurityEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IncidentAlert>().HasIndex(x => new { x.IncidentId, x.AlertId }).IsUnique();
        b.Entity<IncidentAlert>().HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IncidentAlert>().HasOne<Alert>().WithMany().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TimelineEntry>().HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Asset>().HasIndex(x => x.Name).IsUnique();
        b.Entity<ServiceHeartbeat>().HasIndex(x => x.Service).IsUnique();
        b.Entity<PlaybookRun>().HasOne<Incident>().WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PlaybookRun>().HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PlaybookRun>().HasOne<Playbook>().WithMany().HasForeignKey(x => x.PlaybookId).OnDelete(DeleteBehavior.Restrict);
        foreach (var entity in b.Model.GetEntityTypes().ToArray())
        {
            if (typeof(Entity).IsAssignableFrom(entity.ClrType)) b.Entity(entity.ClrType).HasBaseType((Type?)null);
            if (typeof(VersionedEntity).IsAssignableFrom(entity.ClrType))
                b.Entity(entity.ClrType).Property(nameof(VersionedEntity.Version)).IsConcurrencyToken();
            foreach (var p in entity.GetProperties().Where(p => p.ClrType == typeof(string) && p.GetMaxLength() == null && p.GetColumnType() != "jsonb"))
                p.SetMaxLength(4000);
        }
    }

    public void Record(string actor, string action, object resource, string detail = "") =>
        Audit.Add(new AuditEvent { ActorId = actor, Action = action, Resource = resource.ToString() ?? "",
            Detail = detail, TraceId = Activity.Current?.TraceId.ToString() ?? "" });

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (ChangeTracker.Entries().Any(e => e.Entity is AuditEvent or TimelineEntry &&
            e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit and incident timeline entries are append-only.");
        foreach (var e in ChangeTracker.Entries<VersionedEntity>().Where(e => e.State == EntityState.Modified))
        {
            if (e.Entity is ScenarioRun && !e.Property(nameof(ScenarioRun.Status)).IsModified) continue;
            e.Entity.Version++;
        }
        return base.SaveChangesAsync(ct);
    }
}
