using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Discovery;

/// <summary>
/// One annotated member discovered in an assembly scan.
/// </summary>
public sealed record SensitiveDataDiscoveryEntry
{
    /// <summary>Gets the containing type name.</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets the member name.</summary>
    public required string MemberName { get; init; }

    /// <summary>Gets the annotation kind.</summary>
    public required string Annotation { get; init; }

    /// <summary>Gets the regular personal data category, when present.</summary>
    public DataCategory? Category { get; init; }

    /// <summary>Gets the sensitive personal data category, when present.</summary>
    public SensitiveDataCategory? SensitiveCategory { get; init; }

    /// <summary>Gets the sensitivity level.</summary>
    public DataSensitivity Sensitivity { get; init; }

    /// <summary>Gets the retention period in years, when present.</summary>
    public int? RetentionYears { get; init; }

    /// <summary>Gets the retention period in months, when present.</summary>
    public int? RetentionMonths { get; init; }

    /// <summary>Gets the retention policy, when present.</summary>
    public RetentionPolicy? RetentionPolicy { get; init; }
}

