using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

/// <summary>
/// Record of a security incident involving personal data.
/// </summary>
public sealed record IncidentRecord
{
    /// <summary>Unique identifier of the incident.</summary>
    public required string Id { get; init; }

    /// <summary>Timestamp when the incident was detected.</summary>
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Nature of the incident.</summary>
    public IncidentNature Nature { get; init; } = IncidentNature.Other;

    /// <summary>Operational severity of the incident.</summary>
    public IncidentSeverity Severity { get; init; } = IncidentSeverity.Medium;

    /// <summary>Risk level associated with the incident impact.</summary>
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;

    /// <summary>Current lifecycle status of the incident.</summary>
    public IncidentStatus Status { get; init; } = IncidentStatus.Detected;

    /// <summary>Short incident summary.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Categories of personal data affected by the incident.</summary>
    public IReadOnlyList<DataCategory> AffectedData { get; init; } = [];

    /// <summary>Estimated number of affected data subjects.</summary>
    public int? EstimatedAffectedDataSubjects { get; init; }

    /// <summary>Remediation action taken or planned.</summary>
    public string? RemediationAction { get; init; }

    /// <summary>Timestamp when an ANPD notification was generated, when applicable.</summary>
    public DateTimeOffset? AnpdNotificationGeneratedAt { get; init; }
}
