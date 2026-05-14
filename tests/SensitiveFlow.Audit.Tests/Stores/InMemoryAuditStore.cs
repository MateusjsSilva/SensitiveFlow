using System.Collections.Concurrent;
using SensitiveFlow.Core.Enums;
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

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditRecord>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = _records.AsEnumerable();

        if (!string.IsNullOrEmpty(query.Entity))
        {
            result = result.Where(r => r.Entity == query.Entity);
        }
        if (!string.IsNullOrEmpty(query.Operation))
        {
            if (Enum.TryParse<AuditOperation>(query.Operation, out var opEnum))
            {
                result = result.Where(r => r.Operation == opEnum);
            }
        }
        if (!string.IsNullOrEmpty(query.ActorId))
        {
            result = result.Where(r => r.ActorId == query.ActorId);
        }
        if (!string.IsNullOrEmpty(query.DataSubjectId))
        {
            result = result.Where(r => r.DataSubjectId == query.DataSubjectId);
        }
        if (!string.IsNullOrEmpty(query.Field))
        {
            result = result.Where(r => r.Field == query.Field);
        }

        result = Filter(result, query.From, query.To);

        result = query.OrderByDescending
            ? result.OrderByDescending(GetOrderByProperty)
            : result.OrderBy(GetOrderByProperty);

        var finalResult = result.Skip(query.Skip).Take(query.Take).ToList();
        return Task.FromResult<IReadOnlyList<AuditRecord>>(finalResult);

        object GetOrderByProperty(AuditRecord r) => query.OrderBy switch
        {
            "Timestamp" => r.Timestamp,
            "DataSubjectId" => r.DataSubjectId ?? "",
            "Entity" => r.Entity ?? "",
            "Field" => r.Field ?? "",
            "Operation" => r.Operation.ToString(),
            _ => r.Timestamp
        };
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
