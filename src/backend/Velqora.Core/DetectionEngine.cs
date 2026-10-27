namespace Velqora.Core;

public sealed record DetectionMatch(IReadOnlyList<Guid> Evidence, DateTimeOffset Start, DateTimeOffset End);
public static class DetectionEngine
{
    public static DetectionMatch? Evaluate(DetectionRule rule, SecurityEvent trigger, IReadOnlyCollection<SecurityEvent> candidates)
    {
        if (!rule.Enabled || rule.Kind == RuleKind.TelemetryLoss) return null;
        var start = trigger.OccurredAt.AddSeconds(-rule.WindowSeconds);
        var events = candidates.DistinctBy(e => e.Id).Where(e => e.Device == trigger.Device && e.Account == trigger.Account &&
            e.Synthetic == trigger.Synthetic && e.ScenarioRunId == trigger.ScenarioRunId &&
            e.OccurredAt >= start && e.OccurredAt <= trigger.OccurredAt).OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).ToArray();
        SecurityEvent[] evidence;
        switch (rule.Kind)
        {
            case RuleKind.Threshold:
                if (trigger.Type != rule.EventType) return null;
                evidence = events.Where(e => e.Type == rule.EventType).ToArray();
                if (evidence.Length < rule.Threshold) return null;
                break;
            case RuleKind.FailureThenSuccess:
                if (trigger.Type != "auth.success") return null;
                var failures = events.Where(e => e.Type == "auth.failure" && e.OccurredAt < trigger.OccurredAt).ToArray();
                if (failures.Length < rule.Threshold) return null;
                evidence = [.. failures, trigger];
                break;
            case RuleKind.PrivilegeMisuse:
                if (trigger.Type != "privilege.change") return null;
                evidence = [trigger];
                break;
            default: return null;
        }
        return new(evidence.Select(e => e.Id).Distinct().ToArray(), start, trigger.OccurredAt);
    }
    public static bool CanTransition(IncidentStatus from, IncidentStatus to) => from == to || (from, to) switch
    {
        (IncidentStatus.Open, IncidentStatus.Investigating) => true,
        (IncidentStatus.Open, IncidentStatus.Resolved) => true,
        (IncidentStatus.Investigating, IncidentStatus.Contained or IncidentStatus.Resolved) => true,
        (IncidentStatus.Contained, IncidentStatus.Investigating or IncidentStatus.Resolved) => true,
        (IncidentStatus.Resolved, IncidentStatus.Closed or IncidentStatus.Investigating) => true,
        (IncidentStatus.Closed, IncidentStatus.Investigating) => true,
        _ => false
    };
}
