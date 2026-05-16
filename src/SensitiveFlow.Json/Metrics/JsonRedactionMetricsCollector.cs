using System.Diagnostics.Metrics;
using SensitiveFlow.Core;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Json.Metrics;

/// <summary>
/// OpenTelemetry-backed implementation of JSON redaction metrics.
/// </summary>
public class JsonRedactionMetricsCollector : IJsonRedactionMetricsCollector
{
    private static readonly Meter MeterInstance = new("SensitiveFlow.Json", "1.0.0");
    private readonly Counter<long> _redactionCounter;
    private readonly Counter<long> _propertiesSerializedCounter;
    private readonly Histogram<double> _redactionDurationHistogram;

    /// <summary>
    /// Creates a new metrics collector with OpenTelemetry instrumentation.
    /// </summary>
    public JsonRedactionMetricsCollector()
    {
        _redactionCounter = MeterInstance.CreateCounter<long>(
            "sensitiveflow_json_redaction_total",
            description: "Total number of JSON properties redacted");

        _propertiesSerializedCounter = MeterInstance.CreateCounter<long>(
            "sensitiveflow_json_properties_serialized_total",
            description: "Total number of JSON properties serialized");

        _redactionDurationHistogram = MeterInstance.CreateHistogram<double>(
            "sensitiveflow_json_redaction_duration_ms",
            unit: "ms",
            description: "Duration of JSON redaction operations in milliseconds");
    }

    /// <summary>
    /// Records a redaction event for a property.
    /// </summary>
    public void RecordRedaction(string propertyName, OutputRedactionAction action, RedactionContext context = RedactionContext.ApiResponse)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        var tags = new KeyValuePair<string, object?>[]
        {
            new("property_name", propertyName),
            new("action", action.ToString()),
            new("context", context.ToString())
        };

        _redactionCounter.Add(1, tags);
    }

    /// <summary>
    /// Records that a property was serialized without redaction.
    /// </summary>
    public void RecordPropertySerialized()
    {
        _propertiesSerializedCounter.Add(1);
    }

    /// <summary>
    /// Records the duration of a redaction operation.
    /// </summary>
    public void RecordRedactionDuration(double durationMs)
    {
        if (durationMs < 0)
        {
            return;
        }

        _redactionDurationHistogram.Record(durationMs);
    }
}
