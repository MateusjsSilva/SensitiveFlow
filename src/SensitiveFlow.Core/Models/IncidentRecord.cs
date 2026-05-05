using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Models;

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

    /// <summary>
    /// Timestamp when the ANPD notification document was generated.
    /// This records when the notification was prepared, not when it was sent.
    /// </summary>
    public DateTimeOffset? AnpdNotificationGeneratedAt { get; init; }

    /// <summary>
    /// Timestamp when the ANPD notification was effectively sent and confirmed.
    /// <para>
    /// Art. 48 of the LGPD requires notification to the ANPD within a reasonable period.
    /// This field must be set when delivery is confirmed — it is the field that demonstrates
    /// compliance with the notification deadline, not <see cref="AnpdNotificationGeneratedAt"/>.
    /// </para>
    /// </summary>
    public DateTimeOffset? AnpdNotifiedAt { get; init; }
}

