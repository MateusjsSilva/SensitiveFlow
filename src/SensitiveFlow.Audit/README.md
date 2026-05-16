# SensitiveFlow.Audit

Core audit storage, querying, searching, and analysis abstractions for managing audit records at scale.

## Main Components

### Core Interfaces

#### **`IAuditStore`** — Audit record persistence
- `AppendAsync(AuditRecord)` — Insert single record
- `QueryAsync(AuditQuery)` — Query with filtering, pagination, ordering
- `QueryByDataSubjectAsync(string, from, to, skip, take)` — Optimized query for a specific subject
- `QueryStreamAsync(AuditQuery)` — Stream large result sets without materializing in memory

#### **`IAuditStreamQuery`** — Async enumeration for large datasets
- Implements `IAsyncEnumerable<AuditRecord>`
- `CountAsync()` — Get total record count before streaming
- `QueryCriteria` — Access the query parameters used

#### **`IAnonymizationWorkflow`** — GDPR/LGPD right-to-be-forgotten helper
- `AnonymizeByDataSubjectAsync(dataSubjectId, token)` — Replace subject references with anonymization token
- `AnonymizeByQueryAsync(query, token)` — Bulk anonymization by query pattern
- `IsAnonymizedAsync(dataSubjectId)` — Check anonymization status
- `GetAnonymizationTokenAsync(dataSubjectId)` — Retrieve the anonymization token (secure context only)

#### **`IAuditExporter`** — Export in multiple formats
- `ExportAsCsvAsync(records, includeHash)` — CSV with headers
- `ExportAsJsonAsync(records, prettyPrint, includeHash)` — JSON array
- `ExportAsParquetAsync(records, outputPath, includeHash)` — Columnar format for analytics
- `RecordToDictionary(record, includeHash)` — Flexible record serialization

#### **`IAuditSearchIndex`** — Full-text search on audit trails
- `IndexAsync(record)` — Index a single record
- `IndexRangeAsync(records)` — Bulk index multiple records
- `SearchByActorAsync(actorQuery, take)` — Find records by user/actor
- `SearchByIpAsync(ipQuery, take)` — Find records by IP token
- `SearchByEntityAsync(entityQuery, take)` — Find records by entity name
- `SearchAsync(query, take)` — Free-form full-text search
- `RemoveByDataSubjectAsync(dataSubjectId)` — Remove index entries after anonymization
- `ClearAsync()` — Clear entire index

#### **`IAuditAlertingPolicy`** — Anomaly detection
- `DetectAnomaliesAsync(windowMinutes)` — Detect suspicious patterns (bulk deletes, multiple IPs, unusual actors)
- `RegisterRuleAsync(ruleName, detector)` — Custom detection rules
- `UnregisterRuleAsync(ruleName)` — Remove custom rule
- `GetRegisteredRulesAsync()` — List active rules
- `GetAlertsAsync()` — Retrieve unresolved alerts
- `ClearAlertsAsync()` — Clear alerts after resolution

### Query & Model Classes

#### **`AuditQuery`** — Fluent query builder
```csharp
var query = new AuditQuery()
    .ByDataSubject("user-123")
    .ByEntity("Order")
    .ByOperation("Delete")
    .InTimeRange(startDate, endDate)
    .WithPagination(0, 100)
    .OrderByProperty("Timestamp", descending: true);
```

#### **`AuditRecord`** — Immutable audit entry
- `Id` — Unique record identifier (for idempotency)
- `DataSubjectId` — Who the record is about
- `Entity` — Entity type affected
- `Field` — Specific field or property
- `Operation` — Create, Read, Update, Delete, Export, etc.
- `Timestamp` — When the operation occurred
- `ActorId` — Who performed the operation
- `IpAddressToken` — Pseudonymized IP address
- `Details` — Optional additional context
- `PreviousRecordHash`, `CurrentRecordHash` — Integrity chain for tampering detection

#### **`AuditAlert`** — Alert for suspicious patterns
- `Id`, `Severity`, `Message`, `TriggeredAt`
- `DataSubjectIds`, `ActorIds`, `Entities` — Related entities
- `Context` — Additional metadata about the alert

## Usage Patterns

### 1. DI Registration
```csharp
builder.Services.AddScoped<IAuditStore, YourAuditStore>();
builder.Services.AddScoped<IAnonymizationWorkflow, YourAnonymizationWorkflow>();
builder.Services.AddScoped<IAuditExporter, YourAuditExporter>();
builder.Services.AddScoped<IAuditSearchIndex, YourSearchIndex>();
builder.Services.AddScoped<IAuditAlertingPolicy, YourAlertingPolicy>();
```

### 2. Basic Query (Paginated)
```csharp
var records = await auditStore.QueryAsync(
    new AuditQuery()
        .ByDataSubject("user-123")
        .ByEntity("User")
        .InTimeRange(DateTime.Now.AddDays(-30), DateTime.Now)
        .WithPagination(0, 100)
);
```

### 3. Stream Large Result Sets
```csharp
// Process 1M records without allocating memory for all at once
await foreach (var record in auditStore.QueryStreamAsync(
    new AuditQuery()
        .ByDataSubject("user-123")
        .InTimeRange(startDate, endDate)))
{
    await csvWriter.WriteAsync(record);
}
```

### 4. Export Data (Subject Access Request / SAR)
```csharp
var query = new AuditQuery().ByDataSubject("user-123");
var records = auditStore.QueryStreamAsync(query);

// Export as CSV for the data subject
var csv = await exporter.ExportAsCsvAsync(records, includeHash: false);
await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(csv));
```

### 5. Anonymization Workflow (Right to Erasure)
```csharp
// 1. Delete user from database
await userRepository.DeleteAsync(userId);

// 2. Anonymize all audit records
var token = HashBasedAnonymizationToken(userId); // Durable, one-way hash
var count = await workflow.AnonymizeByDataSubjectAsync(userId, token);
Console.WriteLine($"Anonymized {count} audit records");

// 3. Verify deletion
var isDeleted = !await workflow.IsAnonymizedAsync(userId);
```

### 6. Full-Text Search (Incident Response)
```csharp
// Find all records from a specific actor
var records = await searchIndex.SearchByActorAsync("admin@company.com", take: 1000);

// Find records from a suspicious IP
var ipRecords = await searchIndex.SearchByIpAsync("192.168.1.*", take: 500);

// Free-form search
var results = await searchIndex.SearchAsync("DELETE User from 2024-05", take: 100);
```

### 7. Anomaly Detection
```csharp
// Detect suspicious patterns in the last hour
var alerts = await policy.DetectAnomaliesAsync(windowMinutes: 60);

foreach (var alert in alerts)
{
    if (alert.Severity == "Critical")
        await notificationService.SendAlertAsync(alert);
}

// Register custom detection rule
await policy.RegisterRuleAsync("LargeDataExport", async (records) =>
{
    var exports = records.Where(r => r.Operation == AuditOperation.Export);
    if (await exports.CountAsync() > 100)
    {
        yield return new AuditAlert
        {
            Severity = "Warning",
            Message = "Unusual number of exports detected",
            TriggeredAt = DateTimeOffset.UtcNow
        };
    }
});
```

## Audit Record Lifecycle

```
Application Change
    ↓
SensitiveDataAuditInterceptor or ExecuteUpdateAuditedAsync
    ↓
Create AuditRecord
    ↓
IAuditStore.AppendAsync / AppendRangeAsync
    ↓
[Optional] IAuditSearchIndex.IndexAsync (for full-text search)
    ↓
Storage (database, event stream, file, etc.)
    ↓
Queries via IAuditStore.QueryAsync / QueryStreamAsync
    ↓
Export via IAuditExporter for reports/SAR
    ↓
Anonymization via IAnonymizationWorkflow (on right-to-erasure)
    ↓
Analysis via IAuditAlertingPolicy (anomaly detection)
```

## Storage Recommendations

### Low Volume (<10K records/day)
- In-memory store (tests only)
- Simple relational table (SQL Server, PostgreSQL, SQLite)
- No search index needed
- Manual anomaly review

### Medium Volume (10K-100K records/day)
- Relational database with partitioning by DataSubjectId or date
- Indexes: `(DataSubjectId, Timestamp DESC)`, `(ActorId, Timestamp)`
- Optional: Database full-text search (SQL Server FTS, PostgreSQL full-text)
- Scheduled anomaly detection (hourly)

### High Volume (>100K records/day)
- Event streaming (Apache Kafka, Azure Event Hubs)
- Time-series DB (InfluxDB, TimescaleDB) with retention policies
- Dedicated search index (Elasticsearch, Meilisearch)
- Real-time anomaly detection with custom rules
- Stream processing for alerts

### Integrity Requirements
- Always enable `PreviousRecordHash` / `CurrentRecordHash` computation
- Periodically verify audit chain integrity
- Consider separate storage for anonymization tokens (encrypted)

## Retention Policies

See `SensitiveFlow.Retention` for automatic cleanup of audit records based on compliance requirements.
