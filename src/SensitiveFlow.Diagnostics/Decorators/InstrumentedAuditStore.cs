using System.Diagnostics;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Diagnostics.Instruments;

namespace SensitiveFlow.Diagnostics.Decorators;

/// <summary>
/// Decorates an <see cref="IAuditStore"/> with <see cref="System.Diagnostics.ActivitySource"/>
/// spans and <see cref="System.Diagnostics.Metrics.Meter"/> instruments. Query operations are
/// not instrumented — they are not on the <c>SaveChanges</c> hot path.
/// </summary>
public sealed class InstrumentedAuditStore : IBatchAuditStore
{
    private readonly IAuditStore _inner;

    /// <summary>Initializes a new instance.</summary>
    public InstrumentedAuditStore(IAuditStore inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var activity = SensitiveFlowInstruments.ActivitySource.StartActivity(
            "sensitiveflow.audit.append",
            ActivityKind.Internal);
        activity?.SetTag("audit.entity", record.Entity);
        activity?.SetTag("audit.field", record.Field);
        activity?.SetTag("audit.operation", record.Operation.ToString());

        var stopwatch = ValueStopwatch.StartNew();
        try
        {
            await _inner.AppendAsync(record, cancellationToken).ConfigureAwait(false);
            SensitiveFlowInstruments.AuditAppendCount.Add(1);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            SensitiveFlowInstruments.AuditAppendDuration.Record(stopwatch.GetElapsedMilliseconds());
        }
    }

    /// <inheritdoc />
    public async Task AppendRangeAsync(IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
        {
            return;
        }

        using var activity = SensitiveFlowInstruments.ActivitySource.StartActivity(
            "sensitiveflow.audit.append",
            ActivityKind.Internal);
        activity?.SetTag("audit.batch.size", records.Count);

        var stopwatch = ValueStopwatch.StartNew();
        try
        {
            if (_inner is IBatchAuditStore batch)
            {
                await batch.AppendRangeAsync(records, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                foreach (var record in records)
                {
                    await _inner.AppendAsync(record, cancellationToken).ConfigureAwait(false);
                }
            }
            SensitiveFlowInstruments.AuditAppendCount.Add(records.Count);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            SensitiveFlowInstruments.AuditAppendDuration.Record(stopwatch.GetElapsedMilliseconds());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        => _inner.QueryAsync(from, to, skip, take, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        => _inner.QueryByDataSubjectAsync(dataSubjectId, from, to, skip, take, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
        => _inner.QueryAsync(query, cancellationToken);

    private readonly struct ValueStopwatch
    {
        private static readonly double TimestampToMilliseconds = 1000.0 / Stopwatch.Frequency;
        private readonly long _startTimestamp;

        private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;

        public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

        public double GetElapsedMilliseconds() =>
            (Stopwatch.GetTimestamp() - _startTimestamp) * TimestampToMilliseconds;
    }
}
