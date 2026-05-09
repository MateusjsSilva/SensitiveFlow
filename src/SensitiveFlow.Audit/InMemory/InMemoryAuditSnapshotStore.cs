using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IAuditSnapshotStore"/>. Useful for tests and samples;
/// not durable — do not use in production.
/// </summary>
public sealed class InMemoryAuditSnapshotStore : IAuditSnapshotStore
{
    private readonly List<AuditSnapshot> _snapshots = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public Task AppendAsync(AuditSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_lock)
        {
            _snapshots.Add(snapshot);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditSnapshot>> QueryByAggregateAsync(
        string aggregate,
        string aggregateId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregate);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        IReadOnlyList<AuditSnapshot> result;
        lock (_lock)
        {
            result = _snapshots
                .Where(s => s.Aggregate == aggregate && s.AggregateId == aggregateId)
                .Where(s => !from.HasValue || s.Timestamp >= from.Value)
                .Where(s => !to.HasValue || s.Timestamp <= to.Value)
                .OrderBy(s => s.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToList();
        }
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditSnapshot>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);

        IReadOnlyList<AuditSnapshot> result;
        lock (_lock)
        {
            result = _snapshots
                .Where(s => s.DataSubjectId == dataSubjectId)
                .Where(s => !from.HasValue || s.Timestamp >= from.Value)
                .Where(s => !to.HasValue || s.Timestamp <= to.Value)
                .OrderBy(s => s.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToList();
        }
        return Task.FromResult(result);
    }
}
