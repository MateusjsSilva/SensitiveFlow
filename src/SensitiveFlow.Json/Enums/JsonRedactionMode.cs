namespace SensitiveFlow.Json.Enums;

/// <summary>
/// Strategy used to redact a sensitive property when serializing JSON.
/// </summary>
public enum JsonRedactionMode
{
    /// <summary>Do not redact — emit the raw value. Useful as a per-property override.</summary>
    None = 0,

    /// <summary>
    /// Replace the value with a constant string (default: <c>[REDACTED]</c>).
    /// Safe but breaks clients that expect the original value's shape (e.g. a valid email).
    /// </summary>
    Redacted = 1,

    /// <summary>
    /// Apply a partial mask while keeping the value's general shape.
    /// E-mails, phone numbers, and names use the dedicated maskers from
    /// <c>SensitiveFlow.Anonymization</c>; any other string falls back to a generic mask
    /// (first character + asterisks). Non-string values are replaced with <see cref="Redacted"/>.
    /// </summary>
    Mask = 2,

    /// <summary>
    /// Omit the property entirely from the JSON payload. Most secure, but may break
    /// API consumers that expect the key to be present.
    /// </summary>
    Omit = 3,
}
