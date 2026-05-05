namespace SensitiveFlow.Logging.Redaction;

/// <summary>
/// Redacts sensitive values before they reach log sinks.
/// Implement this interface to plug in custom redaction logic.
/// </summary>
public interface ISensitiveValueRedactor
{
    /// <summary>
    /// Redacts the given value, returning a safe representation for logs.
    /// </summary>
    /// <param name="value">The original sensitive value.</param>
    /// <returns>A redacted or masked string safe to log.</returns>
    string Redact(string value);
}
