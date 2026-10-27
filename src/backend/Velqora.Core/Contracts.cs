using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Velqora.Core;

public sealed record EventInput(Guid Id, int SchemaVersion, DateTimeOffset OccurredAt,
    string Source, string Type, string Device, string Account, string IpAddress,
    Dictionary<string, string>? Attributes, bool Synthetic = false);
public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);
public sealed record LoginInput(string Email, string Password);
public sealed record UserInput(string Email, string Password, string Role);
public sealed record AlertUpdate(int Version, AlertStatus Status, string? OwnerId, string? Feedback);
public sealed record IncidentInput(string Title, string Summary, Severity Severity, Guid[] AlertIds);
public sealed record IncidentUpdate(int Version, IncidentStatus Status, Severity Severity, string? OwnerId, string Summary);
public sealed record CommentInput(string Text);
public sealed record EvidenceInput(Guid AlertId);
public sealed record RuleInput(string Name, string Description, RuleKind Kind, string EventType,
    int Threshold, int WindowSeconds, Severity Severity, bool Enabled, int Version = 1);
public sealed record AssetInput(string Name, string Kind, string Owner, string IpAddress, string Account);
public sealed record PlaybookInput(string Name, string Description, string Action, bool Enabled, int Version = 1, string Trigger = "Manual", Severity MinimumSeverity = Severity.High);
public sealed record PlaybookRequest(Guid PlaybookId, Guid IncidentId, Guid AssetId);
public sealed record DecisionInput(int Version, bool Approve);
public sealed record ScenarioInput(string Scenario);
public sealed record ScenarioControl(int Version, string Action);
public sealed class DomainException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}
public static class Guard
{
    public static string Text(string? value, string name, int max = 200, bool required = true)
    {
        var text = value?.Trim() ?? "";
        if ((required && text.Length == 0) || text.Length > max || text.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
            throw new DomainException($"{name} must contain {(required ? "1" : "0")}–{max} valid characters.");
        return text;
    }
    public static void EnumValue<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new DomainException($"Invalid {typeof(T).Name}.");
    }
    public static SecurityEvent Event(EventInput input, DateTimeOffset now)
    {
        if (input.Id == Guid.Empty || input.SchemaVersion != 1) throw new DomainException("A nonempty ID and schemaVersion 1 are required.");
        if (input.OccurredAt > now.AddMinutes(2) || input.OccurredAt < now.AddDays(-7))
            throw new DomainException("Event timestamp must be within the last 7 days and at most 2 minutes in the future.");
        var attributes = input.Attributes ?? [];
        if (attributes.Count > 24 || attributes.Any(x => x.Key.Length is 0 or > 64 || x.Value == null || x.Value.Length > 512))
            throw new DomainException("At most 24 attributes of up to 64/512 characters are supported.");
        if (!IPAddress.TryParse(input.IpAddress, out _)) throw new DomainException("Invalid IP address.");
        var value = new SecurityEvent
        {
            Id = input.Id, SchemaVersion = input.SchemaVersion, OccurredAt = input.OccurredAt.ToUniversalTime(),
            Source = Text(input.Source, "Source", 100), Type = Text(input.Type, "Type", 80),
            Device = Text(input.Device, "Device", 120), Account = Text(input.Account, "Account", 120, false),
            IpAddress = input.IpAddress, Synthetic = input.Synthetic,
            AttributesJson = JsonSerializer.Serialize(attributes.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value))
        };
        var canonical = JsonSerializer.Serialize(new { value.SchemaVersion, value.OccurredAt, value.Source, value.Type,
            value.Device, value.Account, value.IpAddress, value.Synthetic, value.AttributesJson });
        value.PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return value;
    }
    public static void Rule(RuleInput input)
    {
        Text(input.Name, "Name", 120); Text(input.Description, "Description", 1000, false);
        Text(input.EventType, "Event type", 80); EnumValue(input.Kind); EnumValue(input.Severity);
        if (input.Threshold is < 1 or > 10000 || input.WindowSeconds is < 10 or > 86400)
            throw new DomainException("Threshold must be 1–10000 and window 10–86400 seconds.");
    }
    public static string Csv(string? value)
    {
        var text = value ?? "";
        if (text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+') || text.TrimStart().StartsWith('-') || text.TrimStart().StartsWith('@') || text.StartsWith('\t') || text.StartsWith('\r')) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
