using System.Diagnostics.Metrics;
using SensitiveFlow.Core.Diagnostics;

namespace SensitiveFlow.Logging.Metrics;

/// <summary>
/// Collects redaction metrics via OpenTelemetry meters.
/// </summary>
public sealed class RedactionMetricsCollector : IRedactionMetricsCollector
{
    private static readonly Meter Meter = new(SensitiveFlowDiagnostics.MeterName);

    private static readonly Counter<long> RedactionTotalCounter = Meter.CreateCounter<long>(
        name: "sensitiveflow_log_redaction_total",
        unit: "1",
        description: "Total redactions by field name and action");

    private static readonly Counter<long> MessagesScannedCounter = Meter.CreateCounter<long>(
        name: "sensitiveflow_log_messages_scanned_total",
        unit: "1",
        description: "Total log messages scanned for sensitive fields");

    private static readonly Histogram<double> RedactionDurationHistogram = Meter.CreateHistogram<double>(
        name: "sensitiveflow_log_redaction_duration_ms",
        unit: "ms",
        description: "Duration of redaction operations in milliseconds");

    /// <inheritdoc />
    public void RecordRedaction(string fieldName, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        RedactionTotalCounter.Add(1, new KeyValuePair<string, object?>("field_name", fieldName), new KeyValuePair<string, object?>("action", action));
    }

    /// <inheritdoc />
    public void RecordMessageScanned()
    {
        MessagesScannedCounter.Add(1);
    }

    /// <inheritdoc />
    public void RecordRedactionDuration(double duration)
    {
        if (duration >= 0)
        {
            RedactionDurationHistogram.Record(duration);
        }
    }
}
