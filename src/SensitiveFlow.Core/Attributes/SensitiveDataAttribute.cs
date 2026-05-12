using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Marks a property or field as sensitive personal data requiring stricter handling.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute
{
    /// <summary>
    /// Category of sensitive personal data.
    /// Use <see cref="SensitiveDataCategory"/> values — not <see cref="DataCategory"/> — because
    /// sensitive categories carry distinct obligations.
    /// </summary>
    public SensitiveDataCategory Category { get; set; } = SensitiveDataCategory.Other;

    /// <summary>Risk level used by policy decisions, analyzers, and discovery reports.</summary>
    public DataSensitivity Sensitivity { get; set; } = DataSensitivity.High;
}



