using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Attributes;

/// <summary>
/// Marks a property or field as personal data under Art. 5, I of the LGPD.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class PersonalDataAttribute : Attribute
{
    /// <summary>Category of personal data.</summary>
    public DataCategory Category { get; set; } = DataCategory.Other;

    /// <summary>Legal basis that authorizes the processing.</summary>
    public LegalBasis LegalBasis { get; set; } = LegalBasis.Consent;

    /// <summary>Purpose for which the data is processed.</summary>
    public ProcessingPurpose Purpose { get; set; } = ProcessingPurpose.ServiceProvision;
}
