using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Attributes;

/// <summary>
/// Marks a property or field as sensitive personal data under Art. 5, II and Art. 11 of the LGPD.
/// Implies additional obligations and restricted legal bases.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute
{
    public DataCategory Category { get; set; } = DataCategory.Other;
    public SensitiveLegalBasis LegalBasis { get; set; } = SensitiveLegalBasis.ExplicitConsent;
    public ProcessingPurpose Purpose { get; set; } = ProcessingPurpose.ServiceProvision;
}
