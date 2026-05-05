namespace LGPD.NET.Core.Attributes;

/// <summary>
/// Marks a property for automatic deletion when the data subject exercises the right to erasure (Art. 18, IV).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class EraseDataAttribute : Attribute
{
    /// <summary>
    /// When true, the field is anonymized instead of deleted (useful for data kept due to legal obligation).
    /// </summary>
    public bool AnonymizeInsteadOfDelete { get; set; }
}
