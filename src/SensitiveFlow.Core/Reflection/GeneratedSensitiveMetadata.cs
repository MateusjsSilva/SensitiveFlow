using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Reflection;

/// <summary>
/// Source-generated metadata describing sensitive and retention-annotated members.
/// </summary>
public sealed class GeneratedSensitiveType
{
    /// <summary>Initializes a new instance.</summary>
    public GeneratedSensitiveType(
        Type type,
        IReadOnlyList<string> sensitivePropertyNames,
        IReadOnlyList<GeneratedRetentionProperty> retentionProperties)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        SensitivePropertyNames = sensitivePropertyNames ?? Array.Empty<string>();
        RetentionProperties = retentionProperties ?? Array.Empty<GeneratedRetentionProperty>();
    }

    /// <summary>Target type that owns the annotated members.</summary>
    public Type Type { get; }

    /// <summary>Property names annotated with personal or sensitive data attributes.</summary>
    public IReadOnlyList<string> SensitivePropertyNames { get; }

    /// <summary>Retention-annotated properties and their metadata.</summary>
    public IReadOnlyList<GeneratedRetentionProperty> RetentionProperties { get; }
}

/// <summary>Retention metadata for a single property.</summary>
public sealed class GeneratedRetentionProperty
{
    /// <summary>Initializes a new instance.</summary>
    public GeneratedRetentionProperty(string propertyName, int years, int months, RetentionPolicy policy)
    {
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        Years = years;
        Months = months;
        Policy = policy;
    }

    /// <summary>Property name carrying the retention metadata.</summary>
    public string PropertyName { get; }

    /// <summary>Retention period in years.</summary>
    public int Years { get; }

    /// <summary>Retention period in months.</summary>
    public int Months { get; }

    /// <summary>Policy applied when retention expires.</summary>
    public RetentionPolicy Policy { get; }
}
