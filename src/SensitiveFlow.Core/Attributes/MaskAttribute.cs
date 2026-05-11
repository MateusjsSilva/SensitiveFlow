using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Requests masking of an annotated member on output surfaces.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class MaskAttribute : Attribute
{
    /// <summary>Initializes a new instance.</summary>
    public MaskAttribute(MaskKind kind = MaskKind.Generic)
    {
        Kind = kind;
    }

    /// <summary>Gets the requested mask kind.</summary>
    public MaskKind Kind { get; }

    /// <summary>Gets the output action requested by this attribute.</summary>
    public OutputRedactionAction Action => OutputRedactionAction.Mask;
}

