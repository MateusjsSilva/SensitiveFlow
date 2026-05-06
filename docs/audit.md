# Audit

`SensitiveFlow.Audit` provides an immutable audit trail for sensitive data access and mutations.

## AuditRecord

An `AuditRecord` captures a single interaction with a sensitive field:

```csharp
public sealed class AuditRecord
{
    public string Id { get; init; }               // unique identifier (auto-generated)
    public string DataSubjectId { get; set; }     // whose data this is
    public string Entity { get; set; }            // entity/table name
    public string Field { get; set; }             // field/column name
    public AuditOperation Operation { get; set; } // what happened
    public DateTimeOffset Timestamp { get; set; } // when it happened
    public string? ActorId { get; set; }          // who did it (null if anonymous)
    public string? IpAddressToken { get; set; }   // pseudonymized IP (never raw)
    public string? Details { get; set; }          // optional free-text context
}
```

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

## InMemoryAuditStore

The in-memory store is suitable for development and testing. It stores records in a `ConcurrentBag<AuditRecord>` and supports all query filters.

### Registration

```csharp
builder.Services.AddInMemoryAuditStore();
```

This registers `InMemoryAuditStore` as a singleton `IAuditStore`.

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

## Implementing a durable store

For production, implement `IAuditStore` with your persistence layer and register it instead:

```csharp
public sealed class SqlAuditStore : IAuditStore
{
    private readonly AuditDbContext _db;

    public SqlAuditStore(AuditDbContext db) => _db = db;

    public async Task AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        _db.AuditRecords.Add(record);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = int.MaxValue, CancellationToken ct = default)
    {
        var q = _db.AuditRecords.AsQueryable();
        if (from.HasValue) { q = q.Where(r => r.Timestamp >= from.Value); }
        if (to.HasValue)   { q = q.Where(r => r.Timestamp <= to.Value); }
        return await q.OrderBy(r => r.Timestamp).Skip(skip).Take(take).ToListAsync(ct);
    }

    // ...
}

// Registration
builder.Services.AddScoped<IAuditStore, SqlAuditStore>();
```

## DataSubjectId resolution

The EF Core interceptor resolves `DataSubjectId` from the entity by convention, checking the following property names in order:

1. `DataSubjectId`
2. `Id`
3. `UserId`

If none is found, the value defaults to `"unknown"`. For reliable correlation, add a `DataSubjectId` property to every audited entity.
