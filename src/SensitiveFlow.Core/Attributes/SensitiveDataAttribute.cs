using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Marks a property or field as sensitive personal data under high-sensitivity data handling rules in applicable privacy regulations.
/// Implies additional obligations and restricted legal bases.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute
{
    /// <summary>
    /// Category of sensitive personal data .
    /// Use <see cref="SensitiveDataCategory"/> values — not <see cref="DataCategory"/> — because
    /// sensitive categories carry distinct obligations under applicable regulations.
    /// </summary>
    public SensitiveDataCategory Category { get; set; } = SensitiveDataCategory.Other;

    /// <summary>Legal basis for processing sensitive personal data.</summary>
    public SensitiveLegalBasis SensitiveLegalBasis { get; set; } = SensitiveLegalBasis.ExplicitConsent;

    /// <summary>Purpose for which the sensitive data is processed.</summary>
    public ProcessingPurpose Purpose { get; set; } = ProcessingPurpose.ServiceProvision;
}



