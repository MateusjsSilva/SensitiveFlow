namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Output handling action for annotated sensitive values.
/// </summary>
public enum OutputRedactionAction
{
    /// <summary>No explicit output handling was requested.</summary>
    None = 0,

    /// <summary>Replace the value with a fixed placeholder.</summary>
    Redact,

    /// <summary>Mask the value while preserving limited shape.</summary>
    Mask,

    /// <summary>Omit the value from the output surface.</summary>
    Omit,

    /// <summary>Pseudonymize the value before output.</summary>
    Pseudonymize,
}

