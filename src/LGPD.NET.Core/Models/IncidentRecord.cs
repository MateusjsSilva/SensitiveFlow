using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

public sealed record IncidentRecord
{
    public required string Id { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public IncidentSeverity Severity { get; init; } = IncidentSeverity.Medium;
    public IncidentStatus Status { get; init; } = IncidentStatus.Detected;
    public string Summary { get; init; } = string.Empty;
}
