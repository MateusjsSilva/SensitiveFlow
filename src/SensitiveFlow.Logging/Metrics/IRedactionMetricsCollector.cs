namespace SensitiveFlow.Logging.Metrics;

/// <summary>
/// Collects metrics related to sensitive field redaction in logs.
/// </summary>
public interface IRedactionMetricsCollector
{
    /// <summary>
    /// Records that a field was redacted.
    /// </summary>
    /// <param name="fieldName">Name of the redacted field.</param>
    /// <param name="action">Action applied (Redact, Mask, Omit, etc.).</param>
    void RecordRedaction(string fieldName, string action);

    /// <summary>
    /// Records that a log message was scanned for sensitive fields.
    /// </summary>
    void RecordMessageScanned();

    /// <summary>
    /// Records the duration of a redaction operation.
    /// </summary>
    /// <param name="duration">Duration in milliseconds.</param>
    void RecordRedactionDuration(double duration);
}
