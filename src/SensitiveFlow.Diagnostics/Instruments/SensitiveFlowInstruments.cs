using System.Diagnostics;
using System.Diagnostics.Metrics;
using SensitiveFlow.Core.Diagnostics;

namespace SensitiveFlow.Diagnostics.Instruments;

/// <summary>
/// Singleton holders for the shared <see cref="ActivitySource"/> and <see cref="Meter"/>.
/// </summary>
internal static class SensitiveFlowInstruments
{
    public static readonly ActivitySource ActivitySource = new(SensitiveFlowDiagnostics.ActivitySourceName);

    public static readonly Meter Meter = new(SensitiveFlowDiagnostics.MeterName);

    public static readonly Histogram<double> AuditAppendDuration = Meter.CreateHistogram<double>(
        name: SensitiveFlowDiagnostics.AuditAppendDurationName,
        unit: "ms",
        description: "Duration of audit-store append operations.");

    public static readonly Counter<long> AuditAppendCount = Meter.CreateCounter<long>(
        name: SensitiveFlowDiagnostics.AuditAppendCountName,
        unit: "records",
        description: "Audit records appended.");
}
