using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Requests redaction of an annotated member on output surfaces.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class RedactAttribute : Attribute
{
    /// <summary>Gets the output action requested by this attribute.</summary>
    public OutputRedactionAction Action => OutputRedactionAction.Redact;
}

