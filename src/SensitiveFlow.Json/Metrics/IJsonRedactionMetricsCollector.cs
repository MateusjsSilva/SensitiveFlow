using SensitiveFlow.Core;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Json.Metrics;

/// <summary>
/// Tracks JSON redaction metrics for observability.
/// </summary>
public interface IJsonRedactionMetricsCollector
{
    /// <summary>
    /// Records a redaction event for a property.
    /// </summary>
    /// <param name="propertyName">The name of the redacted property.</param>
    /// <param name="action">The redaction action applied.</param>
    /// <param name="context">The redaction context in effect.</param>
    void RecordRedaction(string propertyName, OutputRedactionAction action, RedactionContext context = RedactionContext.ApiResponse);

    /// <summary>
    /// Records that a property was serialized without redaction.
    /// </summary>
    void RecordPropertySerialized();

    /// <summary>
    /// Records the duration of a redaction operation.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    void RecordRedactionDuration(double durationMs);
}
