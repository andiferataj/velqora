namespace Velqora.Core;

public enum Severity { Low, Medium, High, Critical }
public enum AlertStatus { New, Acknowledged, Investigating, Resolved, FalsePositive }
public enum IncidentStatus { Open, Investigating, Contained, Resolved, Closed }
public enum RuleKind { Threshold, FailureThenSuccess, PrivilegeMisuse, TelemetryLoss }
public enum RunStatus { Running, Paused, Completed, Cancelled, Failed }
public enum ApprovalStatus { Pending, Approved, Rejected, Executed, Failed }
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public abstract class VersionedEntity : Entity { public int Version { get; set; } = 1; }
public sealed class SecurityEvent : Entity
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset OccurredAt { get; set; }
    public string Source { get; set; } = "";
    public string Type { get; set; } = "";
    public string Device { get; set; } = "";
    public string Account { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string AttributesJson { get; set; } = "{}";
    public bool Synthetic { get; set; }
    public Guid? ScenarioRunId { get; set; }
    public string PayloadHash { get; set; } = "";
}
public sealed class OutboxMessage : Entity
{
    public Guid EventId { get; set; }
    public string? TraceParent { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ProcessedMessage : Entity { public Guid EventId { get; set; } }
public sealed class DetectionRule : VersionedEntity
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public RuleKind Kind { get; set; }
    public string EventType { get; set; } = "auth.failure";
    public int Threshold { get; set; } = 10;
    public int WindowSeconds { get; set; } = 300;
    public Severity Severity { get; set; } = Severity.High;
    public bool Enabled { get; set; } = true;
}
public sealed class Alert : VersionedEntity
{
    public Guid RuleId { get; set; }
    public int RuleVersion { get; set; }
    public string Title { get; set; } = "";
    public string Device { get; set; } = "";
    public string Account { get; set; } = "";
    public Severity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public string? OwnerId { get; set; }
    public string DeduplicationKey { get; set; } = "";
    public Guid? ScenarioRunId { get; set; }
    public bool Synthetic { get; set; }
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
    public string? Feedback { get; set; }
}
public sealed class AlertEvidence : Entity
{
    public Guid AlertId { get; set; }
    public Guid EventId { get; set; }
}
public sealed class Incident : VersionedEntity
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public Severity Severity { get; set; }
    public IncidentStatus Status { get; set; }
    public string? OwnerId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
public sealed class IncidentAlert : Entity
{
    public Guid IncidentId { get; set; }
    public Guid AlertId { get; set; }
}
public sealed class TimelineEntry : Entity
{
    public Guid IncidentId { get; set; }
    public string ActorId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
}
public sealed class Asset : VersionedEntity
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Workstation";
    public string Owner { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string Account { get; set; } = "";
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool Synthetic { get; set; } = true;
    public bool Isolated { get; set; }
    public bool AccountLocked { get; set; }
}
public sealed class Playbook : VersionedEntity
{
    public string Trigger { get; set; } = "Manual";
    public Severity MinimumSeverity { get; set; } = Severity.High;
    public string Name { get; set; } = "";
    public string Action { get; set; } = "isolate-device";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
public sealed class PlaybookRun : VersionedEntity
{
    public string ActionSnapshot { get; set; } = "";
    public int PlaybookVersion { get; set; }
    public Guid PlaybookId { get; set; }
    public Guid IncidentId { get; set; }
    public Guid AssetId { get; set; }
    public string RequestedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public ApprovalStatus Status { get; set; }
    public string? Result { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ScenarioRun : VersionedEntity
{
    public string Scenario { get; set; } = "login-abuse";
    public string RequestedBy { get; set; } = "";
    public RunStatus Status { get; set; }
    public int Cursor { get; set; }
    public int TotalEvents { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Device { get; set; } = "";
    public string Account { get; set; } = "";
    public string? Error { get; set; }
}
public sealed class AuditEvent : Entity
{
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Detail { get; set; } = "";
    public string TraceId { get; set; } = "";
}
public sealed class ServiceHeartbeat : Entity
{
    public string Service { get; set; } = "worker";
    public DateTimeOffset LastSeenAt { get; set; }
    public string State { get; set; } = "Starting";
}
