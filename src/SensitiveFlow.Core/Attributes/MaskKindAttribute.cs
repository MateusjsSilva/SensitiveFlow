using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Specifies the masking strategy to use when exporting or displaying a sensitive field.
/// Used by the data export mechanism to determine how to mask the field value.
/// Without this attribute, the exporter infers the kind from the property name.
/// </summary>
/// <remarks>
/// <para>
/// Example usage:
/// <code>
/// [PersonalData]
/// [MaskKind(MaskKind.Email)]
/// public string BillingEmail { get; set; }
///
/// [PersonalData]
/// [MaskKind(MaskKind.Phone)]
/// public string EmergencyContact { get; set; }
/// </code>
/// </para>
/// <para>
/// Without <see cref="MaskKindAttribute"/>, the exporter would infer:
/// - <c>BillingEmail</c> as Email (contains "Email")
/// - <c>EmergencyContact</c> as Generic (doesn't match Email/Phone/Name patterns)
/// </para>
/// <para>
/// With <see cref="MaskKindAttribute"/>, the inference is explicit and predictable.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MaskKindAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="MaskKindAttribute"/>.
    /// </summary>
    /// <param name="kind">The masking kind to apply when exporting or displaying the field.</param>
    public MaskKindAttribute(MaskKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// The masking kind to apply.
    /// </summary>
    public MaskKind Kind { get; }
}
