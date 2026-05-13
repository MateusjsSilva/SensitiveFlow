using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Models;

/// <summary>
/// Human-readable representation of a change to a field, showing before/after values.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FieldChange"/> is designed for compliance reports and audit log visualization,
/// where reviewers need to understand "what changed" without necessarily exposing the actual values.
/// </para>
/// <para>
/// For sensitive fields, the before/after values can be redacted, pseudonymized, or summarized
/// (e.g. "Email changed", "Masked Value → Masked Value") to limit exposure while preserving
/// evidence of the change.
/// </para>
/// </remarks>
public sealed record FieldChange
{
    /// <summary>Name of the field that changed.</summary>
    public required string FieldName { get; init; }

    /// <summary>
    /// The value before the change. Can be <c>null</c> for new fields or if redacted.
    /// For sensitive fields, this is typically a masked or pseudonymized representation.
    /// </summary>
    public string? BeforeValue { get; init; }

    /// <summary>
    /// The value after the change. Can be <c>null</c> for deleted fields or if redacted.
    /// For sensitive fields, this is typically a masked or pseudonymized representation.
    /// </summary>
    public string? AfterValue { get; init; }

    /// <summary>
    /// Whether this field was marked sensitive at the time of change.
    /// Used to distinguish between regular and sensitive field changes.
    /// </summary>
    public bool WasSensitive { get; init; }

    /// <summary>
    /// Optional category of the sensitive field (e.g. Contact, Financial, Health).
    /// <c>null</c> for non-sensitive fields.
    /// </summary>
    public string? SensitiveCategory { get; init; }

    /// <summary>
    /// Returns a human-readable summary of the change (e.g. "Email: alice@old.com → alice@new.com").
    /// </summary>
    public override string ToString()
    {
        var before = string.IsNullOrEmpty(BeforeValue) ? "(empty)" : BeforeValue;
        var after = string.IsNullOrEmpty(AfterValue) ? "(empty)" : AfterValue;
        return $"{FieldName}: {before} → {after}";
    }
}

/// <summary>
/// Collection of changes across one or more audit records for a data subject.
/// Useful for generating compliance reports and visualizing audit trails.
/// </summary>
public sealed record AuditRecordDiff
{
    /// <summary>
    /// Unique identifier of this diff record. References the original <see cref="AuditRecord.Id"/>.
    /// </summary>
    public Guid AuditRecordId { get; init; }

    /// <summary>Identifier of the data subject.</summary>
    public required string DataSubjectId { get; init; }

    /// <summary>Entity that was changed.</summary>
    public required string Entity { get; init; }

    /// <summary>Type of operation performed.</summary>
    public AuditOperation Operation { get; init; }

    /// <summary>When the change occurred.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Who performed the change (or <c>null</c> if unknown).</summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Collection of field-level changes. Empty for operations like Access or Anonymize
    /// where no field mutations occurred.
    /// </summary>
    public IReadOnlyList<FieldChange> Changes { get; init; } = [];

    /// <summary>
    /// Returns a multiline summary of all changes.
    /// Example:
    /// <code>
    /// Operation: Update (2025-01-15T10:30:00Z by alice@corp.com)
    /// - Email: alice@old.com → alice@new.com
    /// - Name: Alice Smith → Alice Johnson
    /// </code>
    /// </summary>
    public string ToMultilineString()
    {
        var lines = new List<string>
        {
            $"Operation: {Operation} ({Timestamp:O}" + (ActorId is not null ? $" by {ActorId}" : "") + ")"
        };

        foreach (var change in Changes)
        {
            lines.Add($"- {change}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Returns a JSON-like summary suitable for logs or APIs.
    /// </summary>
    public string ToCompactSummary()
    {
        var changeCount = Changes.Count;
        var sensitiveCount = Changes.Count(c => c.WasSensitive);
        return $"{{entity:{Entity}, operation:{Operation}, changes:{changeCount}, sensitive:{sensitiveCount}}}";
    }
}
