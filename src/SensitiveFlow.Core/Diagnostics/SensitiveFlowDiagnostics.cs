namespace SensitiveFlow.Core.Diagnostics;

/// <summary>
/// Well-known names for the <c>System.Diagnostics</c> instrumentation surface emitted by the
/// SensitiveFlow packages. Wire these into OpenTelemetry via <c>AddMeter</c>/<c>AddSource</c>.
/// </summary>
/// <remarks>
/// Example OpenTelemetry registration:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(m => m.AddMeter(SensitiveFlowDiagnostics.MeterName))
///     .WithTracing(t => t.AddSource(SensitiveFlowDiagnostics.ActivitySourceName));
/// </code>
/// </remarks>
public static class SensitiveFlowDiagnostics
{
    /// <summary>
    /// Name of the <see cref="System.Diagnostics.ActivitySource"/> used by SensitiveFlow.
    /// Spans currently emitted: <c>sensitiveflow.audit.append</c>.
    /// </summary>
    public const string ActivitySourceName = "SensitiveFlow";

    /// <summary>
    /// Name of the <see cref="System.Diagnostics.Metrics.Meter"/> used by SensitiveFlow.
    /// Instruments currently emitted:
    /// <c>sensitiveflow.audit.append.duration</c> (histogram, ms),
    /// <c>sensitiveflow.audit.append.count</c> (counter, records),
    /// <c>sensitiveflow.redact.fields.count</c> (counter, fields).
    /// </summary>
    public const string MeterName = "SensitiveFlow";

    /// <summary>Span name for an audit append operation.</summary>
    public const string AuditAppendActivityName = "sensitiveflow.audit.append";

    /// <summary>Histogram instrument name (milliseconds) for audit append latency.</summary>
    public const string AuditAppendDurationName = "sensitiveflow.audit.append.duration";

    /// <summary>Counter instrument name for audit records appended.</summary>
    public const string AuditAppendCountName = "sensitiveflow.audit.append.count";

    /// <summary>Counter instrument name for fields redacted by the logger.</summary>
    public const string RedactFieldsCountName = "sensitiveflow.redact.fields.count";

    /// <summary>Gauge instrument name for items currently pending in the buffer.</summary>
    public const string BufferPendingItemsName = "sensitiveflow.audit.buffer.pending";

    /// <summary>Counter instrument name for items dropped due to buffer overflow or failure.</summary>
    public const string BufferDroppedItemsName = "sensitiveflow.audit.buffer.dropped";

    /// <summary>Counter instrument name for flush failures in the background worker.</summary>
    public const string BufferFlushFailuresName = "sensitiveflow.audit.buffer.flush_failures";
}
