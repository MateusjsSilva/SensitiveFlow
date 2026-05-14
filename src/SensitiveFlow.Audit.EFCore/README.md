# SensitiveFlow.Audit.EFCore

EF Core implementation of audit storage with database persistence.

## Main Components

### EF Core Audit Store
- **`EfCoreAuditStore<TDbContext>`** — Persists audit records to database
  - Thread-safe, scoped per request
  - Supports sync and async operations
  - Implements `IBatchAuditStore` for efficient insertion
  - Automatic timestamp and actor tracking

### Configuration
- **`AddEfCoreAuditStore<TDbContext>()`** — Registers in DI
- Requires `IAuditRecord` DbSet in context
- Automatically creates tables on migration

## How It Works

### Persistence Flow
```
Application saves entity
    ↓
SensitiveDataAuditInterceptor creates AuditRecord
    ↓
IAuditStore.AppendAsync (or AppendRangeAsync for bulk)
    ↓
EfCoreAuditStore.DbContext.AuditRecords.AddAsync
    ↓
SaveChangesAsync on audit context
    ↓
Persisted to audit_records table
```

### Query Flow
```
Application requests audit trail
    ↓
IAuditStore.QueryAsync(AuditQuery)
    ↓
EfCoreAuditStore builds LINQ from query criteria
    ↓
Executes against database
    ↓
Returns matching records with pagination
```

## Database Schema

Audit records stored in `AuditRecords` table:

```sql
CREATE TABLE AuditRecords (
    Id BIGINT PRIMARY KEY IDENTITY,
    DataSubjectId NVARCHAR(256) NOT NULL,
    Entity NVARCHAR(256) NOT NULL,
    Field NVARCHAR(256) NOT NULL,
    Operation TINYINT NOT NULL,  -- 0=Access, 1=Create, 2=Update, 3=Delete, 4=Export
    Timestamp DATETIMEOFFSET NOT NULL,
    ActorId NVARCHAR(256),
    IpAddressToken NVARCHAR(45),  -- IPv4/IPv6
    Details NVARCHAR(MAX),

    -- Indexes for common queries
    KEY IX_DataSubjectId (DataSubjectId, Timestamp DESC),
    KEY IX_Entity_Operation (Entity, Operation),
    KEY IX_Timestamp (Timestamp DESC),
    KEY IX_ActorId (ActorId)
);
```

## Usage

### Registration
```csharp
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddEfCoreAuditStore<AppDbContext>();
builder.Services.AddSensitiveFlowEFCore();
```

### Separate Audit Database
```csharp
// Main application DB
builder.Services.AddDbContext<AppDbContext>();

// Dedicated audit DB (better isolation)
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlServer(auditConnectionString)
);
builder.Services.AddEfCoreAuditStore<AuditDbContext>();
```

### Querying Audit Trail
```csharp
public sealed class AuditController : ControllerBase
{
    private readonly IAuditStore _auditStore;

    [Authorize]
    [HttpGet("dsar/{userId}")]
    public async Task<IActionResult> GetAuditTrail(string userId)
    {
        var records = await _auditStore.QueryAsync(
            new AuditQuery()
                .ByDataSubjectId(userId)
                .ByDateRange(startDate, endDate)
                .WithPagination(0, 1000)
                .OrderByNewest()
        );

        return Ok(records);
    }
}
```

## Performance Considerations

### Indexes
Default schema includes indexes on:
- `DataSubjectId` (DESC by timestamp) — Data export queries
- `Entity + Operation` — Compliance reports
- `Timestamp` (DESC) — Recent activity
- `ActorId` — User activity tracking

Add custom indexes for your queries:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<AuditRecord>()
        .HasIndex(a => new { a.Entity, a.Timestamp })
        .IsDescending(false, true);
}
```

### Partitioning (Large Volumes)
For tables >100M rows, partition by date:

```sql
-- SQL Server example
CREATE PARTITION FUNCTION AuditByMonth (DATETIMEOFFSET)
AS RANGE RIGHT FOR VALUES (
    '2023-02-01', '2023-03-01', /* ... */
);
```

### Batch Insertion
`IBatchAuditStore` batches multiple records:

```csharp
// InsertAsync calls AppendRangeAsync internally
var records = new List<AuditRecord> { /* ... */ };
await auditStore.AppendRangeAsync(records);
```

Much faster than individual inserts.

### Query Timeouts
```csharp
builder.Services.AddDbContext<AuditDbContext>(options =>
{
    options.UseSqlServer(cs, sqlOptions =>
        sqlOptions.CommandTimeout(300)  // 5 minutes for reports
    );
});
```

## Audit Context Isolation

### Same Database
```csharp
// Audit records in same DB as application data
builder.Services.AddEfCoreAuditStore<AppDbContext>();
```

**Pros**: Simple, transactional consistency
**Cons**: Audit locked if app DB fails

### Separate Database
```csharp
// Application DB
builder.Services.AddDbContext<AppDbContext>();

// Audit DB (independent)
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlServer(auditConnectionString)
);
builder.Services.AddEfCoreAuditStore<AuditDbContext>();
```

**Pros**: Audit survives app DB failure, better isolation
**Cons**: Eventual consistency (async), two connections

## Archival

### Archive Old Records
```csharp
public sealed class AuditArchivalPolicy : IRetentionPolicy
{
    public Type Entity => typeof(AuditRecord);
    public int RetentionDays => 2555;  // 7 years
    
    public Expression<Func<AuditRecord, bool>>? Condition =>
        a => a.Timestamp < DateTime.UtcNow.AddYears(-7);
    
    public RetentionAction Action => RetentionAction.Archive;
}
```

### Cold Storage
Archive to S3 for long-term compliance:
```csharp
// After archival, export to S3
var records = await auditStore.QueryAsync(
    new AuditQuery().ByDateRange(start, archiveDate)
);
await S3Client.PutObjectAsync(new PutObjectRequest
{
    BucketName = "audit-archive",
    Key = $"audit-{archiveDate:yyyy-MM}.json",
    ContentBody = JsonSerializer.Serialize(records)
});
```

## Possible Improvements

1. **Immutable snapshots** — Store audit records as append-only log
2. **Encryption at rest** — Transparent encryption for sensitive audit fields
3. **Query builder UI** — Admin dashboard for audit queries
4. **Real-time alerts** — Stream suspicious patterns via Kafka/SignalR
5. **Compression** — Compress old records for storage efficiency
6. **Replication** — Multi-region audit trail for HA
