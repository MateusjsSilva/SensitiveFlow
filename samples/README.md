# SensitiveFlow Samples

Complete working examples demonstrating SensitiveFlow features across different scenarios.

## Sample Directory

### QuickStart.Sample
**Purpose**: Minimal end-to-end setup with audit logging

**What it demonstrates:**
- Entity annotation with `[PersonalData]` and `[SensitiveData]`
- Automatic auditing via `SensitiveDataAuditInterceptor`
- In-memory audit store for local testing
- Query audit trail with `AuditQuery`

**Key Files:**
- `AppDbContext.cs` — Entity models with sensitive data annotations
- `Program.cs` — Registration and setup

**Technologies:**
- EF Core 10.0
- SQLite (in-memory)

**Run:**
```bash
cd samples/QuickStart.Sample
dotnet run
```

---

### Console.Sample
**Purpose**: Standalone console application demonstrating retention and anonymization

**What it demonstrates:**
- Background task for data retention policies
- Hard delete vs logical delete vs anonymization
- Audit trail for retention actions
- Command-line DSAR (Data Subject Access Request) workflow

**Key Files:**
- `Program.cs` — Retention scheduler setup and manual trigger

**New in preview.4:**
- `IAnonymizationWorkflow` for right-to-erasure
- Durable anonymization tokens

**Technologies:**
- EF Core 10.0
- SQL Server LocalDB

**Run:**
```bash
cd samples/Console.Sample
dotnet run
```

---

### WebApi.Sample
**Purpose**: Full-featured ASP.NET Core REST API with all audit and compliance features

**What it demonstrates:**
- HTTP context integration for audit trails (ActorId, IP)
- Response redaction for API endpoints
- Data Subject Access Request (DSAR) endpoint
- Audit streaming for large exports
- Full-text search on audit records
- Anomaly detection with custom rules
- Audit outbox for reliable delivery

**Key Files:**
- `Program.cs` — DI registration and middleware setup
- `Controllers/CustomersController.cs` — CRUD operations with automatic auditing
- `Controllers/SensitiveFlowController.cs` — Compliance endpoints (DSAR, search, analytics)
- `Infrastructure/SampleDbContext.cs` — Domain models
- `Infrastructure/SampleAuditOutboxPublisher.cs` — Outbox integration

**New in preview.4 endpoints:**
- `GET /api/sensitive-flow/audit/stream` — Stream audit records as CSV
- `GET /api/sensitive-flow/audit/search` — Full-text search on audit trail
- `GET /api/sensitive-flow/audit/anomalies` — Detect suspicious patterns
- `POST /api/sensitive-flow/audit/export/{userId}` — Export all data for subject
- `DELETE /api/sensitive-flow/audit/erase/{userId}` — Right-to-erasure workflow

**Technologies:**
- ASP.NET Core 8.0+
- EF Core 10.0
- SQL Server
- Newtonsoft.Json for API serialization

**Run:**
```bash
cd samples/WebApi.Sample
dotnet run
```

**API Endpoints:**
- `GET /api/customers` — List all customers (redacted)
- `POST /api/customers` — Create customer (audited)
- `GET /api/customers/{id}` — Get customer details (redacted)
- `PUT /api/customers/{id}` — Update customer (audited)
- `DELETE /api/customers/{id}` — Delete customer (audited)
- `GET /api/sensitive-flow/audit` — Audit trail query
- `GET /api/sensitive-flow/audit/stream` — Stream large audit exports
- `GET /api/sensitive-flow/audit/search?query=...` — Full-text search
- `GET /api/sensitive-flow/audit/anomalies` — Detect anomalies
- `POST /api/sensitive-flow/audit/export/{userId}` — DSAR export
- `DELETE /api/sensitive-flow/audit/erase/{userId}` — Right-to-erasure

---

### MinimalApi.Sample
**Purpose**: Minimal APIs alternative to WebApi.Sample

**What it demonstrates:**
- Same functionality as WebApi.Sample using Minimal APIs
- Cleaner endpoint definitions
- Inline middleware

**Key Files:**
- `Program.cs` — All endpoint definitions
- `Infrastructure/SampleDbContext.cs` — Domain models

**Technologies:**
- ASP.NET Core 8.0+
- EF Core 10.0
- SQL Server
- System.Text.Json for serialization

**Run:**
```bash
cd samples/MinimalApi.Sample
dotnet run
```

---

### Redis.Sample
**Purpose**: Custom `ITokenStore` implementation using Redis for distributed pseudonymization

**What it demonstrates:**
- Custom persistent token store for reversible pseudonymization
- Redis integration with `StackExchange.Redis`
- Concurrent `GetOrCreateTokenAsync` with Lua atomicity
- Token reuse across application instances

**Key Files:**
- `RedisTokenStore.cs` — Thread-safe Redis implementation of `ITokenStore`
- `Program.cs` — Redis connection and registration

**Technologies:**
- EF Core 10.0
- Redis (local or Docker)
- StackExchange.Redis

**Prerequisites:**
```bash
# Start Redis locally with Docker
docker run -d -p 6379:6379 redis:latest
```

**Run:**
```bash
cd samples/Redis.Sample
dotnet run
```

---

### Redis.Microservice.Sample
**Purpose**: Distributed pseudonymization service using Redis token store

**What it demonstrates:**
- Microservice-ready pseudonymization endpoint
- Thread-safe token generation and lookup
- Reversible data masking for analytics
- Health checks for Redis connectivity

**Key Files:**
- `Controllers/PseudonymizationController.cs` — REST API for pseudonymization
- `Program.cs` — Service registration

**Endpoints:**
- `POST /api/pseudonymize` — Pseudonymize multiple values
- `POST /api/reverse` — Reverse pseudonyms back to original
- `GET /health` — Health check status

**Technologies:**
- ASP.NET Core 8.0+
- Redis token store
- StackExchange.Redis

**Run:**
```bash
docker run -d -p 6379:6379 redis:latest
cd samples/Redis.Microservice.Sample
dotnet run
```

---

## Feature Coverage by Sample

| Feature | QuickStart | Console | WebApi | MinimalApi | Redis | Redis.Microservice |
|---------|-----------|---------|--------|------------|-------|------------------|
| Core annotations | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Auditing | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Bulk operations | | ✅ | ✅ | ✅ | | |
| Retention | | ✅ | ✅ | ✅ | | |
| Incremental scheduling | | ✅ | ✅ | ✅ | | |
| Parallel retention | | ✅ | ✅ | ✅ | | |
| Retention analytics | | ✅ | ✅ | ✅ | | |
| Re-anonymization | | ✅ | ✅ | ✅ | | |
| Archive tiering | | ✅ | ✅ | ✅ | | |
| Retention notifications | | ✅ | ✅ | ✅ | | |
| Retention reporting | | ✅ | ✅ | ✅ | | |
| Data export (DSAR) | | ✅ | ✅ | ✅ | | |
| Right-to-erasure | | ✅ | ✅ | ✅ | | |
| Async streaming | | | ✅ | ✅ | | |
| Full-text search | | | ✅ | ✅ | | |
| Anomaly detection | | | ✅ | ✅ | | |
| Response redaction | | | ✅ | ✅ | | |
| Logging redaction | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| JSON serialization | | | ✅ | ✅ | | |
| Analyzer ILogger<T> support | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Analyzer attribute exclusion | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Analyzer cross-assembly | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Analyzer custom masking methods | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Pseudonymization | | | | | ✅ | ✅ |
| Custom token store | | | | | ✅ | ✅ |
| Health checks | | | ✅ | ✅ | ✅ | ✅ |
| Audit outbox | | | ✅ | ✅ | | |

## New in Preview.4

All samples have been updated to demonstrate preview.4 features:

### Audit Features

#### 1. Async Streaming
- Stream large audit datasets without memory materialization
- Perfect for exporting 100K+ records
- Demonstrated in `WebApi.Sample` — `GET /api/sensitive-flow/audit/stream`

#### 2. Anonymization Workflow
- Durable right-to-erasure with anonymization tokens
- Audit trail for erasure requests
- Demonstrated in `WebApi.Sample` — `DELETE /api/sensitive-flow/audit/erase/{userId}`

#### 3. Multi-Format Export
- CSV and JSON export with integrity hashing
- Streaming exports for large datasets
- Demonstrated in `WebApi.Sample` — `POST /api/sensitive-flow/audit/export/{userId}`

#### 4. Full-Text Search Index
- Search audit records by actor, IP, entity, or free-form text
- In-memory implementation suitable for testing
- Production: use Elasticsearch or similar
- Demonstrated in `WebApi.Sample` — `GET /api/sensitive-flow/audit/search`

#### 5. Anomaly Detection
- Pluggable detection rules
- Built-in detectors: bulk deletes, multiple IPs, suspicious patterns
- Demonstrated in `WebApi.Sample` — `GET /api/sensitive-flow/audit/anomalies`

### Diagnostics Features

#### 6. Custom Metrics
- Track sensitive field access, redaction duration, compliance violations
- Integrate with OpenTelemetry and Prometheus
- Example: `CustomMetricsProvider` integration in middleware

#### 7. Compliance Reporting
- Audit frequency reports (operations, entities, actors)
- Data subject coverage analysis (audit trail gaps)
- Retention compliance verification (deletion tracking)
- Example: `ComplianceReportService` endpoint in WebApi.Sample

#### 8. Performance Baselines
- Define target performance values
- Detect regressions automatically
- Receive context-aware optimization recommendations
- Example: `PerformanceBaselineService` in startup validation

#### 9. Alert Rule Templates
- 6 pre-built Prometheus alert rules (high latency, bulk deletes, etc.)
- Exportable as YAML for alerting systems
- Configured in `AlertRuleTemplates` class

#### 10. Query Optimization
- Analyze query patterns from audit operations
- Suggest database indexes based on frequency
- Generate SQL index creation statements
- Example: `QueryOptimizationAdvisor` in performance monitoring

### Logging Features

#### 11. Structured Property Redaction
- Redact sensitive keys in log scope dictionaries
- Configuration via `StructuredPropertyRedactor` in options
- Prevents API keys, passwords from appearing in logs
- Example: exclude "ApiKey", "Password" from structured logging

#### 12. Audit Trail Correlation
- Automatic injection of CorrelationId into log scopes
- Simplifies request tracing across service boundaries
- Uses `SensitiveFlowCorrelation.Current` for context
- Example: `AuditCorrelationScope` wrapping logger instances

#### 13. Redaction Performance Metrics
- OpenTelemetry counters tracking redaction frequency
- Histograms for redaction operation duration
- Built-in collector: `RedactionMetricsCollector`
- Enables monitoring redaction overhead in production

#### 14. Custom Masking Rules
- Pluggable `IMaskingStrategy` interface for flexible masking
- Built-in strategies: phone, creditcard, ipaddress
- Configurable via `MaskingStrategyRegistry`
- Extensible for domain-specific masking rules

#### 15. Log Sampling
- Probabilistic sampling of logs containing sensitive data
- Reduces log volume in high-throughput scenarios
- Configured via `LogSamplingFilter` with sampling rate
- Smart filtering: non-sensitive logs always preserved

### Analyzer Enhancements

#### 16. Generic `ILogger<T>` Support
- Analyzer SF0001 now supports `ILogger<T>` generic type parameters
- Detects logging of sensitive data through generic logger instances
- No configuration needed—works out of the box with `ILogger<MyClass>`

#### 17. Attribute-Based Suppression
- Properties marked with `[SensitiveFlowIgnoreAttribute]` are excluded from diagnostics
- Suppresses SF0001 (logging), SF0002 (responses), and SF0006 (missing redaction)
- Enables intentional opt-out for special cases where sensitive data bypasses normal flows

#### 18. Cross-Assembly Analysis
- Analyzers detect `[PersonalData]` and `[SensitiveData]` from referenced assemblies
- Enables analysis of shared libraries with sensitive data annotations
- Automatically works when analyzing projects that reference data libraries

#### 19. Custom Masking Method Recognition
- Built-in recognition of methods containing: `Mask`, `Redact`, `Anonymize`, `Pseudonymize`, `Hash`
- Case-insensitive matching—`MaskEmail()`, `REDACT()`, `AnonymizePhone()` all recognized
- Suppresses SF0001 and SF0002 automatically without configuration

### Retention Enhancements

#### 20. Incremental Scheduling
- Track last successful run per policy to avoid reprocessing
- Thread-safe policy state via `RetentionRunTracker`
- Reduces redundant data processing in scheduled jobs

#### 17. Parallel Policy Execution
- Run multiple retention batches concurrently
- `ParallelRetentionExecutor` via `Task.WhenAll`
- Merged execution reports for cross-policy insights

#### 18. Retention Analytics
- Collect execution metrics: anonymized fields, deletion pending count, duration
- `RetentionAnalyticsCollector` with trend summarization
- Track peak runs, averages, and historical trends

#### 19. Selective Re-anonymization
- Re-anonymize entities matching a condition on-demand
- `RetentionReAnonymizer` with predicate filtering
- Enables remediation without waiting for retention expiration

#### 20. Archive Tiering
- Abstract storage layer for expired entities
- `InMemoryRetentionArchiveProvider` for testing
- Production: integrate with S3, Azure Blob, or other cold storage

#### 21. Notification Templates
- Configurable alert templates with placeholder substitution
- Support for Email, Slack, and Webhook channels
- Template.Format(report) fills {AnonymizedCount}, {DeletePendingCount}, {RunAt}

#### 22. Retention Analytics Reporting
- Generate formatted reports: text, CSV, and JSON
- `RetentionReportGenerator` for export and analysis
- Supports detailed execution history and trend summaries

### Role-Based Redaction (Core improvement)
- Different redaction per user role (Admin, Support, Customer)
- Context-aware masking via `RedactionContext` enum
- Demonstrated in `WebApi.Sample` response payloads

### Composite Data Subject IDs (Core improvement)
- Multi-key entity identification (e.g., CustomerId + OrderId)
- Audit trail key combines all properties
- Optional—use when single DataSubjectId insufficient

## Quick Start

### 1. Understand the basics
```bash
cd samples/QuickStart.Sample
dotnet run
```

### 2. See a full REST API
```bash
cd samples/WebApi.Sample
dotnet run
# Browse http://localhost:5000/swagger
```

### 3. Explore async patterns
Look at `WebApi.Sample/Controllers/SensitiveFlowController.cs`:
- Line ~150: `StreamAuditAsCsvAsync` — async streaming
- Line ~170: `SearchAudit` — full-text search
- Line ~190: `AnalyzeAnomalies` — anomaly detection

### 4. Test data export workflow
```bash
cd samples/WebApi.Sample
# Make requests to these endpoints:
curl http://localhost:5000/api/sensitive-flow/audit/export/user-123
curl http://localhost:5000/api/sensitive-flow/audit/erase/user-123
```

## Common Tasks

### Run all samples
```bash
for dir in samples/*/; do
  (cd "$dir" && dotnet run --no-build) &
done
```

### Test with multiple targets
```bash
cd samples/WebApi.Sample
dotnet build -c Release -f net8.0
dotnet build -c Release -f net9.0
dotnet build -c Release -f net10.0
```

### Enable SQL Server audit logging
Edit `appsettings.json` and add:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

## Troubleshooting

### SQL Server connection error
```bash
# Verify LocalDB is running
sqllocaldb info
sqllocaldb start mssqllocaldb

# Or use Docker
docker run -e SA_PASSWORD=Password123! -p 1433:1433 mcr.microsoft.com/mssql/server:latest
```

### Redis connection error
```bash
# Start Redis with Docker
docker run -d -p 6379:6379 redis:latest

# Or check if running
redis-cli ping
```

### Port already in use
Edit `appsettings.json`:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5001"
      }
    }
  }
}
```

## Architecture Decisions

### In-Memory vs Database Audit Store
- **QuickStart/Tests**: `InMemoryAuditStore` (no external deps)
- **Production**: `EfCoreAuditStore` with SQL Server/PostgreSQL
- **Key difference**: Interface-based `IAuditStore` allows swapping implementations

### In-Memory vs Elasticsearch Search
- **Tests/Dev**: `InMemoryAuditSearchIndex` (fast, no external deps)
- **Production (10K-100K records)**: SQL Server FTS or PostgreSQL FTS
- **Production (1M+ records)**: Elasticsearch, Meilisearch
- **Same interface**: `IAuditSearchIndex` allows production upgrade path

### Synchronous vs Asynchronous
- Core audit capture is async-only (prevents deadlocks in ASP.NET)
- Some helpers have sync aliases (for console/Windows services)
- Deprecated sync methods marked with `[Obsolete]` warnings
- Always use async in web contexts

## Next Steps

1. **Pick a sample** that matches your use case
2. **Read the controllers** to understand the patterns
3. **Copy the models** and adapt to your domain
4. **Update ConnectionStrings** in `appsettings.json`
5. **Run migrations** to create audit tables
6. **Test endpoints** to see auditing in action

## Documentation Links

- [SensitiveFlow.Core README](../src/SensitiveFlow.Core/README.md) — Attributes & models
- [SensitiveFlow.Audit README](../src/SensitiveFlow.Audit/README.md) — Audit storage & querying
- [SensitiveFlow.EFCore README](../src/SensitiveFlow.EFCore/README.md) — Automatic auditing
- [SensitiveFlow.AspNetCore README](../src/SensitiveFlow.AspNetCore/README.md) — HTTP context
- [SensitiveFlow.Analyzers README](../src/SensitiveFlow.Analyzers/README.md) — Compile-time checks
- [PACKAGES_OVERVIEW.md](../PACKAGES_OVERVIEW.md) — Complete package reference

## Support

For issues or questions:
1. Check relevant package README
2. Review sample source code
3. Run tests: `dotnet test`
4. Report issues on GitHub
