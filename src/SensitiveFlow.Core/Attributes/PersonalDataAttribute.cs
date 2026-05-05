using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Marks a property or field as personal data.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class PersonalDataAttribute : Attribute
{
    /// <summary>Category of personal data.</summary>
    public DataCategory Category { get; set; } = DataCategory.Other;
}


