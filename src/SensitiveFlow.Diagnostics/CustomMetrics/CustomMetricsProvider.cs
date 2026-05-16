using System.Diagnostics.Metrics;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Diagnostics.CustomMetrics;

/// <summary>
/// Provides domain-specific metrics for sensitive data operations.
/// </summary>
public sealed class CustomMetricsProvider
{
    private readonly Meter _meter;
    private readonly Counter<long> _sensitiveFieldsAccessedCounter;
    private readonly Histogram<double> _redactionDurationHistogram;
    private readonly Counter<long> _complianceViolationsCounter;

    /// <summary>
    /// Initializes a new instance with custom metrics.
    /// </summary>
    public CustomMetricsProvider()
    {
        _meter = new Meter(SensitiveFlowDiagnostics.MeterName);

        _sensitiveFieldsAccessedCounter = _meter.CreateCounter<long>(
            name: "sensitiveflow.sensitive_fields_accessed",
            unit: "count",
            description: "Number of sensitive data fields accessed");

        _redactionDurationHistogram = _meter.CreateHistogram<double>(
            name: "sensitiveflow.redaction.duration",
            unit: "ms",
            description: "Time spent in redaction operations");

        _complianceViolationsCounter = _meter.CreateCounter<long>(
            name: "sensitiveflow.compliance_violations",
            unit: "count",
            description: "Number of detected compliance violations");
    }

    /// <summary>
    /// Record a sensitive field access.
    /// </summary>
    public void RecordSensitiveFieldAccess(string fieldName, string entity)
    {
        _sensitiveFieldsAccessedCounter.Add(
            delta: 1,
            new KeyValuePair<string, object?>("field", fieldName),
            new KeyValuePair<string, object?>("entity", entity));
    }

    /// <summary>
    /// Record redaction operation duration.
    /// </summary>
    public void RecordRedactionDuration(double durationMilliseconds, string redactionKind)
    {
        _redactionDurationHistogram.Record(
            value: durationMilliseconds,
            new KeyValuePair<string, object?>("kind", redactionKind));
    }

    /// <summary>
    /// Record a compliance violation.
    /// </summary>
    public void RecordComplianceViolation(string violationType, string description)
    {
        _complianceViolationsCounter.Add(
            delta: 1,
            new KeyValuePair<string, object?>("violation_type", violationType),
            new KeyValuePair<string, object?>("description", description));
    }
}
