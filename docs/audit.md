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
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
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

## Audit redaction context

By default, audit records do not store raw field values. If a model uses contextual audit redaction, the EF Core interceptor honors it:

```csharp
[PersonalData(Category = DataCategory.Contact)]
[Redaction(Audit = OutputRedactionAction.Mask)]
public string Email { get; set; } = string.Empty;

[SensitiveData(Category = SensitiveDataCategory.Other)]
[Redaction(Audit = OutputRedactionAction.Omit)]
public string InternalNote { get; set; } = string.Empty;
```

`Audit = Omit` suppresses the per-field audit record. `Redact`, `Mask`, and `Pseudonymize` write a protected value into `AuditRecord.Details`; pseudonymization uses the registered `IPseudonymizer` when one is available and falls back to the default redaction marker otherwise.

## Audit outbox

`SensitiveFlow.Audit` ships a concrete in-memory outbox for tests/local development. Production systems must use a durable audit outbox to ensure reliable delivery to downstream systems (e.g., SIEM, compliance dashboards, data lakes).

### In-memory outbox (tests only)

```csharp
builder.Services.AddAuditStore<MyDurableAuditStore>();
builder.Services.AddInMemoryAuditOutbox();  // Deprecated – use for tests/dev only
```

⚠️ `InMemoryAuditOutbox` is **deprecated for production**. It loses all enqueued records on process restart and is not suitable for compliance/audit scenarios. The `SensitiveFlowConfigurationValidator` will emit `SF-CONFIG-013` if it detects an in-memory outbox outside a Development environment.

### Durable outbox with EF Core (production-ready)

For production, use the EF Core-backed durable outbox with transactional guarantees:

```bash
dotnet add package SensitiveFlow.Audit.EFCore.Outbox
```

```csharp
// Register durable audit store + durable outbox with automatic dispatcher
builder.Services.AddEfCoreAuditStore(opt => opt.UseSqlServer(...));
builder.Services.AddEfCoreAuditOutbox(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(1);
    options.BatchSize = 100;
    options.MaxAttempts = 5;
});

// Register a publisher to deliver outbox records downstream
builder.Services.AddScoped<IAuditOutboxPublisher, MySiemPublisher>();
```

The durable outbox provides **at-least-once delivery** guarantees:
- Records enqueued and audit store writes happen in a single `SaveChanges` transaction
- Failed deliveries are retried with exponential backoff (configurable)
- Dead-lettered entries (max retries exceeded) are queryable for inspection
- The `AuditOutboxDispatcher` automatically detects and polls pending entries

### Custom durable outbox

If you need to integrate with a different backend (e.g. Apache Kafka, AWS SQS), implement `IDurableAuditOutbox`:

```csharp
public sealed class KafkaAuditOutbox : IDurableAuditOutbox
{
    // Implement: EnqueueAsync, DequeueBatchAsync, MarkProcessedAsync, MarkFailedAsync
}

builder.Services.AddAuditStore<MyAuditStore>();
builder.Services.AddAuditOutbox<KafkaAuditOutbox>();
```

### Audit outbox guarantees and defaults

| Outbox | Intended use | Delivery guarantee | Survives restart | Notes |
| --- | --- | --- | --- | --- |
| `InMemoryAuditOutbox` | tests/local development | best effort / at-most-once | no | Deprecated for production. Diagnostics emit `SF-CONFIG-013` outside Development, and the audit-outbox health check reports `Degraded`. |
| `SensitiveFlow.Audit.EFCore.Outbox` | production durable delivery | at-least-once | yes | EF Core storage, dispatcher retries, and dead-letter state. |
| custom `IDurableAuditOutbox` | application-specific backend | defined by your implementation | should be yes | Register at least one `IAuditOutboxPublisher`; otherwise diagnostics emit `SF-CONFIG-014` as an **Error** (validation throws unless `FailOnError = false`). |

Dispatcher defaults:

| Option | Default |
| --- | --- |
| `PollInterval` | `1s` |
| `BatchSize` | `100` |
| `MaxAttempts` | `5` |
| `BackoffStrategy` | `Exponential` |
| `DeadLetterAfterMax` | `true` |

Custom durable outboxes must implement `MarkDeadLetteredAsync(...)` in addition to enqueue, dequeue, processed, and failed acknowledgement methods. Register at least one `IAuditOutboxPublisher` so the dispatcher has a delivery target.

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

> **DI lifetime:** `AddAuditStoreRetry` preserves the original `IAuditStore` lifetime. `AddBufferedAuditStore` requires a Singleton store because the buffer owns a background worker. Use `AddEfCoreAuditStore(...)` or another Singleton durable store before enabling buffering.

### Buffer health checks

`BufferedAuditStore` exposes `GetHealth()` for monitoring and health-check endpoints:

```csharp
var health = bufferedStore.GetHealth();
// health.PendingItems      — records still waiting in the buffer
// health.DroppedItems      — total records dropped (overflow or channel closure)
// health.FlushFailures     — total flush failures in the background worker
// health.IsFaulted         — whether the background worker has failed permanently
// health.BackgroundFailure — exception message if faulted
```

OpenTelemetry metrics are emitted automatically:
- `sensitiveflow.audit.buffer.pending` (gauge) — current queue depth
- `sensitiveflow.audit.buffer.dropped` (counter) — records dropped
- `sensitiveflow.audit.buffer.flush_failures` (counter) — flush failures

Wire them into your OpenTelemetry setup with `AddMeter("SensitiveFlow")`.

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

`SensitiveFlow.Audit` ships an `InMemoryAuditSnapshotStore` for tests and samples. For production, use `SensitiveFlow.Audit.Snapshots.EFCore`:

```csharp
builder.Services.AddEfCoreAuditSnapshotStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Snapshots")));
```

This registers `EfCoreAuditSnapshotStore<TContext>` backed by a dedicated `SnapshotDbContext` with indexes optimized for aggregate and data-subject queries.
