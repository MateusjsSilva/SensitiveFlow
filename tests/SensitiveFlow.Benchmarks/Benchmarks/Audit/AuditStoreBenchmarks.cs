using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Benchmarks.Audit;

/// <summary>
/// Benchmarks for audit store operations performance.
///
/// Measures:
/// - Single audit record write latency
/// - Query performance for audit trail retrieval
/// - Memory allocation during audit operations
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class AuditStoreBenchmarks
{
    private IAuditStore _auditStore = null!;
    private readonly List<string> _dataSubjectIds = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Using in-memory audit store for benchmarking
        _auditStore = new InMemoryAuditStore();

        // Generate test data
        for (int i = 0; i < 100; i++)
        {
            _dataSubjectIds.Add($"user_{i}");
        }

        // Pre-populate with test records
        Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await _auditStore.AppendAsync(new AuditRecord
                {
                    Id = Guid.NewGuid(),
                    DataSubjectId = _dataSubjectIds[i % _dataSubjectIds.Count],
                    Entity = "Customer",
                    Field = $"Field{i}",
                    Operation = AuditOperation.Update,
                    Timestamp = DateTimeOffset.UtcNow.AddHours(-i),
                    ActorId = "admin@company.com"
                });
            }
        }).Wait();
    }

    /// <summary>
    /// Benchmark: Write single audit record
    /// </summary>
    [Benchmark(Description = "Write single audit record")]
    public async Task BenchmarkWriteSingleRecord()
    {
        var record = new AuditRecord
        {
            DataSubjectId = _dataSubjectIds[Random.Shared.Next(_dataSubjectIds.Count)],
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "admin@company.com"
        };

        await _auditStore.AppendAsync(record);
    }

    /// <summary>
    /// Benchmark: Query audit records by DataSubjectId
    /// </summary>
    [Benchmark(Description = "Query by DataSubjectId")]
    public async Task BenchmarkQueryByDataSubject()
    {
        var subjectId = _dataSubjectIds[0];
        var records = await _auditStore.QueryByDataSubjectAsync(subjectId);
    }

    /// <summary>
    /// Benchmark: Query audit records by date range
    /// </summary>
    [Benchmark(Description = "Query by date range")]
    public async Task BenchmarkQueryByDateRange()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        var records = await _auditStore.QueryAsync(from: from, to: to);
    }

    /// <summary>
    /// Benchmark: Query using structured query builder
    /// </summary>
    [Benchmark(Description = "Query by entity")]
    public async Task BenchmarkQueryByEntity()
    {
        var query = new AuditQuery().ByEntity("Customer").WithPagination(0, 50);
        var records = await _auditStore.QueryAsync(query);
    }
}

/// <summary>
/// Simple in-memory audit store for benchmarking
/// </summary>
public class InMemoryAuditStore : IAuditStore
{
    private readonly List<AuditRecord> _records = new();

    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            _records.Add(record);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            var query = _records.AsEnumerable();

            if (from.HasValue)
                query = query.Where(r => r.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(r => r.Timestamp <= to.Value);

            return Task.FromResult<IReadOnlyList<AuditRecord>>(
                query.Skip(skip).Take(take).ToList()
            );
        }
    }

    public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            var query = _records.Where(r => r.DataSubjectId == dataSubjectId);

            if (from.HasValue)
                query = query.Where(r => r.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(r => r.Timestamp <= to.Value);

            return Task.FromResult<IReadOnlyList<AuditRecord>>(
                query.Skip(skip).Take(take).ToList()
            );
        }
    }

    public Task<IReadOnlyList<AuditRecord>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            var results = _records.AsEnumerable();

            if (query.Entity != null)
                results = results.Where(r => r.Entity == query.Entity);

            if (query.Field != null)
                results = results.Where(r => r.Field == query.Field);

            if (query.DataSubjectId != null)
                results = results.Where(r => r.DataSubjectId == query.DataSubjectId);

            if (query.ActorId != null)
                results = results.Where(r => r.ActorId == query.ActorId);

            return Task.FromResult<IReadOnlyList<AuditRecord>>(
                results.Skip(query.Skip).Take(query.Take).ToList()
            );
        }
    }
}
