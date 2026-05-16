using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.InMemory;

/// <summary>
/// Simple in-memory full-text search index for audit records.
/// Thread-safe. Suitable for testing and small datasets only.
/// For production, use Elasticsearch or similar.
/// </summary>
public sealed class InMemoryAuditSearchIndex : IAuditSearchIndex
{
    private readonly object _lock = new();
    private readonly Dictionary<string, AuditRecord> _records = new();
    private readonly Dictionary<string, List<string>> _actorIndex = new();
    private readonly Dictionary<string, List<string>> _ipIndex = new();
    private readonly Dictionary<string, List<string>> _entityIndex = new();

    /// <inheritdoc />
    public Task IndexAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _records[record.Id.ToString()] = record;
            IndexField(_actorIndex, record.ActorId, record.Id.ToString());
            IndexField(_ipIndex, record.IpAddressToken, record.Id.ToString());
            IndexField(_entityIndex, record.Entity, record.Id.ToString());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task IndexRangeAsync(
        IAsyncEnumerable<AuditRecord> records,
        CancellationToken cancellationToken = default)
    {
        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            await IndexAsync(record, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> SearchByActorAsync(
        string actorQuery,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var matches = _actorIndex
                .Where(kvp => kvp.Key != null && kvp.Key.Contains(actorQuery, StringComparison.OrdinalIgnoreCase))
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .Take(take)
                .Select(id => _records[id])
                .ToList();

            return Task.FromResult<IReadOnlyList<AuditRecord>>(matches);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> SearchByIpAsync(
        string ipQuery,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var matches = _ipIndex
                .Where(kvp => kvp.Key != null && kvp.Key.Contains(ipQuery, StringComparison.OrdinalIgnoreCase))
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .Take(take)
                .Select(id => _records[id])
                .ToList();

            return Task.FromResult<IReadOnlyList<AuditRecord>>(matches);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> SearchByEntityAsync(
        string entityQuery,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var matches = _entityIndex
                .Where(kvp => kvp.Key.Contains(entityQuery, StringComparison.OrdinalIgnoreCase))
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .Take(take)
                .Select(id => _records[id])
                .ToList();

            return Task.FromResult<IReadOnlyList<AuditRecord>>(matches);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> SearchAsync(
        string query,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var matches = _records.Values
                .Where(r =>
                    (r.ActorId?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.IpAddressToken?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    r.Entity.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.Field.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (r.Details?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(take)
                .ToList();

            return Task.FromResult<IReadOnlyList<AuditRecord>>(matches);
        }
    }

    /// <inheritdoc />
    public Task RemoveByDataSubjectAsync(
        string dataSubjectId,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var toRemove = _records
                .Where(kvp => kvp.Value.DataSubjectId == dataSubjectId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in toRemove)
            {
                _records.Remove(id);
                RemoveFromAllIndexes(id);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _records.Clear();
            _actorIndex.Clear();
            _ipIndex.Clear();
            _entityIndex.Clear();
        }

        return Task.CompletedTask;
    }

    private void IndexField(Dictionary<string, List<string>> index, string? fieldValue, string recordId)
    {
        if (string.IsNullOrWhiteSpace(fieldValue))
        {
            return;
        }

        if (!index.ContainsKey(fieldValue))
        {
            index[fieldValue] = new List<string>();
        }

        if (!index[fieldValue].Contains(recordId))
        {
            index[fieldValue].Add(recordId);
        }
    }

    private void RemoveFromAllIndexes(string recordId)
    {
        RemoveFromIndex(_actorIndex, recordId);
        RemoveFromIndex(_ipIndex, recordId);
        RemoveFromIndex(_entityIndex, recordId);
    }

    private static void RemoveFromIndex(Dictionary<string, List<string>> index, string recordId)
    {
        var keysToRemove = index
            .Where(kvp => kvp.Value.Contains(recordId))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            index[key].Remove(recordId);
            if (index[key].Count == 0)
            {
                index.Remove(key);
            }
        }
    }
}
