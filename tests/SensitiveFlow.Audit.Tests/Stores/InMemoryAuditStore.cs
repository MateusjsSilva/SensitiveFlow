using System.Collections.Concurrent;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests.Stores;

/// <summary>
/// Thread-safe in-memory audit store for tests only.
/// Mappings are lost when the process exits — do NOT use in production.
/// For production, implement <see cref="IAuditStore"/> backed by a durable sink (SQL, MongoDB, etc.).
/// </summary>
internal sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentBag<AuditRecord> _records = new();

    /// <inheritdoc />
    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records.Add(record);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = Filter(_records, from, to)
            .OrderBy(r => r.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditRecord>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);

        var result = Filter(_records, from, to)
            .Where(r => r.DataSubjectId == dataSubjectId)
            .OrderBy(r => r.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditRecord>>(result);
    }

    private static IEnumerable<AuditRecord> Filter(
        IEnumerable<AuditRecord> records,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (from.HasValue)
        {
            records = records.Where(r => r.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            records = records.Where(r => r.Timestamp <= to.Value);
        }

        return records;
    }
}
