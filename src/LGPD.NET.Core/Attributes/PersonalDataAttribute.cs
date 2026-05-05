using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Attributes;

/// <summary>
/// Marks a property or field as personal data under Art. 5, I of the LGPD.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class PersonalDataAttribute : Attribute
{
    public DataCategory Category { get; set; } = DataCategory.Other;
    public LegalBasis LegalBasis { get; set; } = LegalBasis.Consent;
    public ProcessingPurpose Purpose { get; set; } = ProcessingPurpose.ServiceProvision;
}
