# SensitiveFlow.Audit

Core audit storage and querying abstractions for managing audit records.

## Main Components

### Interfaces
- **`IAuditStore`** — Basic audit record persistence
  - `AppendAsync(AuditRecord)`: Insert single record
  - `QueryAsync(AuditQuery)`: Query records with filtering/pagination

- **`IBatchAuditStore`** — Optimized batch insertion
  - `AppendRangeAsync(IEnumerable<AuditRecord>)`: Insert multiple records atomically
  - Inherited from `IAuditStore`

- **`IAuditQuery`** — Query builder for audit trails
  - `ByDataSubjectId(string)`: Filter by subject
  - `ByEntity(string)`: Filter by entity type
  - `ByOperation(AuditOperation)`: Filter by operation type
  - `ByDateRange(DateTimeOffset, DateTimeOffset)`: Filter by timestamp
  - `WithPagination(skip, take)`: Pagination
  - `OrderByNewest()`: Sort descending by timestamp

### Models
- **`AuditQuery`** — Query criteria for filtering audit records
- **`AuditRecord`** — Audit trail entry (defined in Core)

## Implementations

### InMemoryAuditStore
Simple in-memory implementation for testing and lightweight scenarios. **Not thread-safe**, suitable for unit tests only.

```csharp
var store = new InMemoryAuditStore();
await store.AppendAsync(new AuditRecord { ... });
var records = await store.QueryAsync(new AuditQuery());
```

## Usage Patterns

### DI Registration
```csharp
builder.Services.AddScoped<IAuditStore, YourAuditStore>();
builder.Services.AddSensitiveFlowEFCore(); // Uses IAuditStore from DI
```

### Querying
```csharp
var records = await auditStore.QueryAsync(
    new AuditQuery()
        .ByDataSubjectId("user-123")
        .ByDateRange(startDate, endDate)
        .WithPagination(0, 100)
        .OrderByNewest()
);
```

### Data Export Workflow
1. Receive data export request for a subject
2. Query audit via `IAuditStore.QueryAsync` with `ByDataSubjectId`
3. Combine with data export from `IDataSubjectExportService`
4. Redact using `[Redaction(Export=...)]` rules
5. Return export to requester

## Audit Record Lifecycle

```
Application Change
    ↓
SensitiveDataAuditInterceptor or ExecuteUpdateAuditedAsync
    ↓
Create AuditRecord (with redaction details)
    ↓
IAuditStore.AppendAsync/AppendRangeAsync
    ↓
Storage (database, file, event stream, etc.)
    ↓
IAuditStore.QueryAsync for retrieval
    ↓
Output with configured redaction per context
```

## Storage Recommendations

### Low Volume (<10K records/day)
- Use `InMemoryAuditStore` for tests
- Simple relational table (SQL Server, PostgreSQL, SQLite)

### Medium Volume (10K-100K records/day)
- Relational database with partitioning by DataSubjectId or date
- Async batch insertion via `IBatchAuditStore`
- Index: `(DataSubjectId, Timestamp DESC)`

### High Volume (>100K records/day)
- Event sourcing (Apache Kafka, Azure Event Hubs)
- Time-series database (InfluxDB, TimescaleDB)
- Audit log streaming with async persister

## Retention Policies

See `SensitiveFlow.Retention` for automatic cleanup of audit records.

## Possible Improvements

1. **Async query streaming** — For large result sets, stream records instead of materialization
2. **Full-text search** — Search by actor, IP, or entity name
3. **Anonymization workflow** — Helper to anonymize all records for deleted subject
4. **Export formats** — CSV, JSON, Parquet export for reports
5. **Alerting** — Detect suspicious patterns (bulk deletes, multiple IPs per subject)
