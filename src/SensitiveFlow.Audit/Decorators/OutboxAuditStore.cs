using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Decorators;

/// <summary>
/// Audit-store decorator that appends to the primary store and then enqueues the
/// same record in an audit outbox for asynchronous downstream delivery.
/// </summary>
public sealed class OutboxAuditStore : IBatchAuditStore
{
    private readonly IAuditStore _inner;
    private readonly IAuditOutbox _outbox;

    /// <summary>Initializes a new instance.</summary>
    public OutboxAuditStore(IAuditStore inner, IAuditOutbox outbox)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    }

    /// <inheritdoc />
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        await _inner.AppendAsync(record, cancellationToken);
        await _outbox.EnqueueAsync(record, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AppendRangeAsync(IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (_inner is IBatchAuditStore batch)
        {
            await batch.AppendRangeAsync(records, cancellationToken);
        }
        else
        {
            foreach (var record in records)
            {
                await _inner.AppendAsync(record, cancellationToken);
            }
        }

        foreach (var record in records)
        {
            await _outbox.EnqueueAsync(record, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _inner.QueryAsync(from, to, skip, take, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _inner.QueryByDataSubjectAsync(dataSubjectId, from, to, skip, take, cancellationToken);
}
