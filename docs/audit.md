# Audit

`SensitiveFlow.Audit` provides an immutable audit trail for sensitive data access and mutations.

## AuditRecord

An `AuditRecord` captures a single interaction with a sensitive field:

```csharp
public sealed class AuditRecord
{
    public Guid Id { get; init; }                 // unique identifier (auto-generated)
    public required string DataSubjectId { get; init; } // whose data this is
    public required string Entity { get; init; }        // entity/table name
    public required string Field { get; init; }         // field/column name
    public AuditOperation Operation { get; init; }      // what happened (defaults to Access)
    public DateTimeOffset Timestamp { get; init; }      // when it happened
    public string? ActorId { get; init; }               // who did it (null if anonymous)
    public string? IpAddressToken { get; init; }        // pseudonymized IP (never raw)
    public string? Details { get; init; }               // optional free-text context
}
```

> **`Id` is a `Guid`**, not a string. When you persist it (e.g. in a custom store), call `.ToString()` to convert. `EfCoreAuditStore` does this automatically.
>
> **`Operation` defaults to `AuditOperation.Access`** so that read-only audit records can omit it. Set it explicitly for write, delete, or export events.

### AuditOperation

| Value | Description |
|-------|-------------|
| `Access` | Field was read |
| `Create` | Record was inserted |
| `Update` | Field value was changed |
| `Delete` | Record was removed |
| `Export` | Data was exported |
| `Anonymize` | Data was anonymized |
| `Pseudonymize` | Data was pseudonymized |

## IAuditStore

The `IAuditStore` interface is the only dependency required by the EF Core interceptor:

```csharp
public interface IAuditStore
{
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = int.MaxValue,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = int.MaxValue,
        CancellationToken cancellationToken = default);
}
```

## Implementing a durable store

`IAuditStore` has no built-in implementation — you own the persistence layer so audit records go exactly where your infrastructure requires.
Audit records must survive process restarts. An in-memory store is not suitable for production.

If you use EF Core, `SensitiveFlow.Audit.EFCore` ships a ready-made `EfCoreAuditStore<TContext>` plus an `AddEfCoreAuditStore()` DI extension.

```csharp
using SensitiveFlow.Audit.EFCore.Extensions;

// Option A: Map it on top of your existing application DbContext
builder.Services.AddEfCoreAuditStore<MyDbContext>();

// Option B: Use a dedicated DbContext just for audit logs
builder.Services.AddEfCoreAuditStore(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("AuditStorage")));
```

If you need to store records elsewhere (e.g. MongoDB, Dapper), you can implement `IAuditStore` yourself:

```csharp
public sealed class CustomAuditStore : IAuditStore
{
    // Inject your infrastructure
    public CustomAuditStore(...) { }

    public async Task AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        // Add to your custom storage
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken ct = default)
    {
        // Query from your custom storage
        return [];
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken ct = default)
    {
        // Query from your custom storage
        return [];
    }
}

// Registration
builder.Services.AddAuditStore<CustomAuditStore>();
```

### Querying records

```csharp
// All records in the last 7 days
var records = await auditStore.QueryAsync(from: DateTimeOffset.UtcNow.AddDays(-7));

// All records for a specific data subject
var subject = await auditStore.QueryByDataSubjectAsync("customer-42");

// Paginated
var page = await auditStore.QueryAsync(skip: 20, take: 10);
```

Records are returned in ascending timestamp order.

## Batch appends

If your audit store can persist multiple records in one logical operation, implement `IBatchAuditStore` in addition to `IAuditStore`.

`SensitiveDataAuditInterceptor` will call `AppendRangeAsync` once per `SaveChanges` when the store supports batching. That keeps the audit write path closer to the entity save and avoids one roundtrip per sensitive field.

## Retrying transient failures

Audit appends sit in the hot path of `SaveChanges`. A brief network blip or lock contention against the audit store would otherwise cascade into a `SaveChangesAsync` failure. Wrap your store with the bundled `RetryingAuditStore`:

```csharp
builder.Services.AddEfCoreAuditStore<MyDbContext>();
builder.Services.AddAuditStoreRetry(options =>
{
    options.MaxAttempts       = 3;
    options.InitialDelay      = TimeSpan.FromMilliseconds(100);
    options.BackoffMultiplier = 2.0;
});
```

The decorator only retries appends — `QueryAsync` is left alone. `ArgumentException` and `OperationCanceledException` are treated as terminal (input or cancellation, not transient) and never retried.

## Buffered high-volume appends

For workloads where audit writes are too expensive to keep directly on the `SaveChanges` path, wrap the durable store with `BufferedAuditStore`:

```csharp
builder.Services.AddEfCoreAuditStore<MyDbContext>();
builder.Services.AddAuditStoreRetry();
builder.Services.AddBufferedAuditStore(options =>
{
    options.Capacity = 10_000;
    options.MaxBatchSize = 250;
});
```

The buffer is bounded. When it fills, appends wait until the background worker drains space instead of dropping audit records. The worker flushes via `IBatchAuditStore.AppendRangeAsync` when the inner store supports batching, otherwise it falls back to one append per record.

> **Trade-off:** this is an in-process buffer. A crash can lose records accepted into memory but not yet flushed to the durable store, and queries may not see records still waiting in the queue. Use it only when that latency/throughput trade-off is explicit for the application.

### Combining Retry and Diagnostics

`AddAuditStoreRetry`, `AddBufferedAuditStore`, and `AddSensitiveFlowDiagnostics` are decorators that wrap the registered `IAuditStore`. **Order matters:**

```csharp
// One span covers the entire retry cycle (retry is invisible to the trace):
builder.Services.AddEfCoreAuditStore<MyDbContext>();
builder.Services.AddSensitiveFlowDiagnostics();  // outer: one span per logical operation
builder.Services.AddAuditStoreRetry();           // inner: retries before bubbling

// One span per attempt (each retry shows as a separate child span):
builder.Services.AddEfCoreAuditStore<MyDbContext>();
builder.Services.AddAuditStoreRetry();           // inner: retries
builder.Services.AddSensitiveFlowDiagnostics();  // outer: one span wraps each attempt

// Buffered appends with retries applied to the durable flush:
builder.Services.AddEfCoreAuditStore<MyDbContext>();
builder.Services.AddAuditStoreRetry();           // inner: retry durable writes
builder.Services.AddBufferedAuditStore();        // outer: enqueue quickly, flush in background
```

> **DI lifetime:** both decorators preserve the lifetime of the original `IAuditStore` registration. Mixing `AddAuditStore<T>()` (Scoped) with `AddEfCoreAuditStore()` (Singleton) in the same container is not recommended — pick one registration path per application.

### Tests

For tests, implement `IAuditStore` inline or use the `SensitiveFlow.TestKit` conformance suite, which exercises the public contract for any custom store:

```csharp
public sealed class MyAuditStoreTests : AuditStoreContractTests
{
    protected override Task<IAuditStore> CreateStoreAsync()
        => Task.FromResult<IAuditStore>(new MyAuditStore(/*...*/));
}
```

## DataSubjectId resolution

The EF Core interceptor requires the entity to expose a stable subject identifier in one of these properties (checked in order):

1. `DataSubjectId`
2. `UserId` (legacy alias)

If neither exists, or if the value is null/empty, the interceptor throws `InvalidOperationException` at `SaveChanges` time. Falling back to a database-generated `Id` was removed because EF providers can assign auto-increment keys before the interceptor runs — the resulting audit row would be tagged with a value that has no meaning to the data subject.

## Purging old audit records (IAuditLogRetention)

The audit log itself accumulates personal data (subject IDs, actor IDs) and falls under the same retention obligations as the data it records. `SensitiveFlow.Audit.EFCore` registers `IAuditLogRetention` alongside the store:

```csharp
// Registered automatically by AddEfCoreAuditStore:
// services.TryAddSingleton<IAuditLogRetention>(...);

// Inject it in a background job or hosted service:
public class AuditPurgeJob(IAuditLogRetention retention)
{
    public Task RunAsync(CancellationToken ct)
        => retention.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddYears(-2), ct);
}
```

`PurgeOlderThanAsync` uses `ExecuteDeleteAsync` (a single SQL `DELETE` statement) on relational providers, falling back to a materialise-and-remove approach on providers that do not support bulk deletes (e.g. InMemory in tests).

---

## Aggregate snapshots — `AuditSnapshot`

`AuditRecord` is a per-field trail: one row per modified property. For some domains, a per-aggregate trail is more useful — a single entry per change carrying the serialized "before" and "after" state of the aggregate. `AuditSnapshot` and `IAuditSnapshotStore` provide that shape.

When to prefer snapshots:

- **Aggregates whose fields are only meaningful together** (e.g. an `Address` with `Street` / `City` / `Zip`). Three field-level rows hide the relationship; one snapshot row preserves it.
- **Reviewers expect a "diff" view.** Showing `Before` and `After` side by side is friendlier than reconstructing the state from N field-level entries.
- **Domains with strong audit/regulator pressure** where the inspector wants to see exactly what the record looked like at every step.

When to prefer the per-field `AuditRecord`:

- **High-frequency partial updates** where each field changes independently.
- **Storage cost matters and you only ever care about which fields changed.**

The snapshot model is independent of the per-field model — you can use both, neither, or only one.

```csharp
// Build the snapshot from your aggregate. Use SensitiveFlow.Json options to ensure
// sensitive fields are already redacted before serialization, especially if the
// snapshot store has weaker access controls than the primary store.
var redactingOptions = new JsonSerializerOptions().WithSensitiveDataRedaction();

var snapshot = new AuditSnapshot
{
    DataSubjectId = customer.DataSubjectId,
    Aggregate = nameof(Customer),
    AggregateId = customer.Id.ToString(),
    Operation = AuditOperation.Update,
    BeforeJson = JsonSerializer.Serialize(beforeState, redactingOptions),
    AfterJson  = JsonSerializer.Serialize(customer,    redactingOptions),
    ActorId = currentUser.Id,
    IpAddressToken = ipToken,
};

await snapshotStore.AppendAsync(snapshot);
```

`SensitiveFlow.Audit` ships an `InMemoryAuditSnapshotStore` for tests and samples. Production use needs a durable backing — implement `IAuditSnapshotStore` against your database of choice (a dedicated EF-backed implementation may follow in a future preview).
