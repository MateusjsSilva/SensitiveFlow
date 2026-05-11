using System.Diagnostics.Metrics;
using SensitiveFlow.Core.Diagnostics;

namespace SensitiveFlow.Audit.Outbox;

/// <summary>Metrics emitted by SensitiveFlow audit infrastructure.</summary>
public static class SensitiveFlowAuditDiagnostics
{
    private static readonly Meter Meter = new(SensitiveFlowDiagnostics.MeterName);

    /// <summary>Number of audit outbox records enqueued.</summary>
    public static readonly Counter<long> OutboxEnqueued = Meter.CreateCounter<long>(
        "sensitiveflow.audit.outbox.enqueued",
        unit: "records",
        description: "Audit records enqueued in an outbox.");

    /// <summary>Number of audit outbox records dispatched successfully.</summary>
    public static readonly Counter<long> OutboxDispatched = Meter.CreateCounter<long>(
        "sensitiveflow.audit.outbox.dispatched",
        unit: "records",
        description: "Audit outbox records dispatched successfully.");

    /// <summary>Number of audit outbox dispatch failures.</summary>
    public static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>(
        "sensitiveflow.audit.outbox.failed",
        unit: "records",
        description: "Audit outbox dispatch failures.");

    /// <summary>Number of audit outbox entries dead-lettered.</summary>
    public static readonly Counter<long> OutboxDeadLettered = Meter.CreateCounter<long>(
        "sensitiveflow.audit.outbox.deadlettered",
        unit: "records",
        description: "Audit outbox records that reached max delivery attempts.");

    /// <summary>Records one enqueue.</summary>
    public static void RecordEnqueued(long count = 1) => OutboxEnqueued.Add(count);

    /// <summary>Records one successful dispatch.</summary>
    public static void RecordDispatched(long count = 1) => OutboxDispatched.Add(count);

    /// <summary>Records one dispatch failure.</summary>
    public static void RecordFailed(long count = 1) => OutboxFailed.Add(count);

    /// <summary>Records one dead-letter event.</summary>
    public static void RecordDeadLettered(long count = 1) => OutboxDeadLettered.Add(count);
}
