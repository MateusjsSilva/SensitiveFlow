namespace SensitiveFlow.Json.Enums;

/// <summary>
/// Strategy used for sensitive JSON properties whose CLR type is not <see cref="string"/>.
/// </summary>
public enum JsonNonStringRedactionMode
{
    /// <summary>
    /// Emit a string placeholder such as <c>[NUMBER_REDACTED]</c>.
    /// This is explicit, but changes the JSON value type.
    /// </summary>
    Placeholder = 0,

    /// <summary>
    /// Emit <c>null</c> for the property value.
    /// This avoids fake values such as <c>0</c>, but requires nullable-friendly clients.
    /// </summary>
    Null = 1,

    /// <summary>
    /// Omit the property entirely from the JSON payload.
    /// This is the strictest option, but can break clients that expect the key.
    /// </summary>
    Omit = 2,
}
