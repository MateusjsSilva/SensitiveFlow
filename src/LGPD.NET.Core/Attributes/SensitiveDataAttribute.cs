using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Attributes;

/// <summary>
/// Marks a property or field as sensitive personal data under Art. 5, II and Art. 11 of the LGPD.
/// Implies additional obligations and restricted legal bases.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute
{
    /// <summary>
    /// Category of sensitive personal data (Art. 5, II of the LGPD).
    /// Use <see cref="SensitiveDataCategory"/> values — not <see cref="DataCategory"/> — because
    /// sensitive categories carry distinct obligations under Art. 11.
    /// </summary>
    public SensitiveDataCategory Category { get; set; } = SensitiveDataCategory.Other;

    /// <summary>Legal basis for processing sensitive personal data.</summary>
    public SensitiveLegalBasis SensitiveLegalBasis { get; set; } = SensitiveLegalBasis.ExplicitConsent;

    /// <summary>Purpose for which the sensitive data is processed.</summary>
    public ProcessingPurpose Purpose { get; set; } = ProcessingPurpose.ServiceProvision;
}
