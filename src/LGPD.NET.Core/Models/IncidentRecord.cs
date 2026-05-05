using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

public sealed record IncidentRecord
{
    public required string Id { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public IncidentNature Nature { get; init; } = IncidentNature.Other;
    public IncidentSeverity Severity { get; init; } = IncidentSeverity.Medium;
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;
    public IncidentStatus Status { get; init; } = IncidentStatus.Detected;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<DataCategory> AffectedData { get; init; } = [];
    public int? EstimatedAffectedDataSubjects { get; init; }
    public string? RemediationAction { get; init; }
    public DateTimeOffset? AnpdNotificationGeneratedAt { get; init; }
}
