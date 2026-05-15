# AI skill: using SensitiveFlow correctly

Use this document as instructions for an AI coding agent that needs to add or modify SensitiveFlow usage in a .NET application.

## Goal

Help the application control personal data flows at runtime: audit writes, redact serialized output/logs, pseudonymize reversible identifiers, and evaluate retention policies.

## Required reading order

1. `docs/package-reference.md` — all packages and their APIs
2. `docs/getting-started.md` — quick setup
3. `docs/attributes.md` — how to classify sensitive/personal data
4. `BENCHMARK_SUMMARY.md` — performance impact and overhead analysis
5. **Relevant feature docs:**
   - audit: `docs/audit.md`
   - EF Core: `docs/efcore.md`
   - ASP.NET Core: `docs/aspnetcore.md`
   - anonymization: `docs/anonymization.md`
   - JSON: `docs/json.md`
   - logging: `docs/logging.md`
   - retention: `docs/retention.md`
6. **Advanced/reference docs:**
   - analyzers: `docs/analyzers.md`
   - testkit: `docs/testkit.md`
   - diagnostics: `docs/diagnostics.md`
   - policies & discovery: `docs/policies-discovery-health.md`
   - database providers: `docs/database-providers.md`
   - alternative backends: `docs/backends-example.md`
   - outbox pattern: `docs/outbox-example.md`
   - DTO pattern: `docs/dto-pattern.md`
7. **Troubleshooting:**
   - troubleshooting: `docs/troubleshooting.md`
   - common pitfalls: `docs/common-pitfalls.md`

## Performance expectations

Before implementing, understand the overhead:

- **Retention policy evaluation:** 27 nanoseconds (negligible)
- **Audit write (in-memory):** <0.5ms per record
- **Audit write (database):** 2-4ms per record (depends on DB speed)
- **JSON masking:** <1ms per object
- **Logging redaction:** <1ms per message
- **Redis pseudonymization:** 2-5ms per operation
- **Typical API request overhead:** 2-4% (3.5-6ms added to 15-160ms requests)

⚠️ **If your database is slow (>100ms latency), audit overhead can reach 5-10%. Monitor accordingly.**

See [BENCHMARK_SUMMARY.md](../BENCHMARK_SUMMARY.md) for full performance analysis.

## Decision tree

### If the app uses EF Core and needs audit

Use:

```csharp
builder.Services.AddEfCoreAuditStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Audit")));
builder.Services.AddAuditStoreRetry();
builder.Services.AddSensitiveFlowEFCore();
```

Then add `SensitiveDataAuditInterceptor` to the application `DbContext`.

Rules:

- Prefer `SaveChangesAsync`.
- Ensure auditable entities have `DataSubjectId` or `UserId`.
- Annotate sensitive fields with `[PersonalData]` or `[SensitiveData]`.
- Do not store raw IP addresses in `AuditRecord`.
- **Monitor:** If audit database becomes slow, SaveChanges overhead increases proportionally.

### If the app exposes HTTP APIs

Use:

```csharp
builder.Services.AddSensitiveFlowAspNetCore();
app.UseSensitiveFlowAudit();
```

Rules:

- Register a durable `ITokenStore` before middleware uses `TokenPseudonymizer`.
- Place middleware early enough that downstream handlers can read the audit context.

### If the app serializes DTOs/entities to JSON

Use:

```csharp
options.SerializerOptions.WithSensitiveDataRedaction();
```

Rules:

- Prefer DTOs designed for output.
- Add `SensitiveDataAssert.DoesNotLeak(...)` tests for representative responses.

### If the app logs personal data

Use SensitiveFlow logging only as a guardrail, not as permission to log raw personal data.

Rules:

- Prefer not logging sensitive values.
- If a value must be logged, mask or pseudonymize before logging.
- Keep analyzer warnings enabled.

### If the app pseudonymizes values

Use `TokenPseudonymizer` when reversibility is required.

Rules:

- `ITokenStore` must be durable (EF Core or Redis).
- Add `TokenStoreContractTests` for custom stores.
- Add a unique constraint on original value and token in database-backed stores.
- Consider `AddCachingTokenStore(...)` for repeated hot values after accepting the in-memory exposure trade-off.
- **For distributed systems:** use `SensitiveFlow.TokenStore.Redis` instead of EF Core store (2-5ms per operation, supports concurrent access across instances).
- **Monitor:** If Redis becomes slow or unavailable, pseudonymization latency increases proportionally.

Use `HmacPseudonymizer` when deterministic non-reversible tokens are enough.

Rules:

- Secret must be strong and stable.
- Rotation invalidates previous tokens unless handled deliberately.
- Performance: <0.1ms per mask (significantly faster than token store).

### If the app needs retention

Use:

```csharp
builder.Services.AddRetention();
builder.Services.AddRetentionExecutor();
```

Rules:

- Run retention from a scheduled job.
- The library evaluates/mutates fields; application code owns row deletion.
- Persist changes after executor/evaluator work.
- See `docs/retention.md` for retention policies and enforcement patterns.

### If the app needs data export or erasure (GDPR)

Use `DataSubjectExporter` for export and `DataSubjectEraser` for deletion.

Rules:

- Entity must have `DataSubjectId` or `UserId` for filtering.
- Use durable `ITokenStore` to recover pseudonymized values.
- Return structured export format per `docs/anonymization.md`.
- See `docs/anonymization.md` for fingerprinting, token caching, and bulk operations.

### If the app uses ASP.NET Core with multiple instances

Use `SensitiveFlow.TokenStore.Redis` for distributed token store.

Rules:

- Redis must be accessible from all instances.
- Token cache should be tested with `TokenStoreContractTests`.
- Performance: 2-5ms per operation (acceptable for pseudonymization).
- See `docs/backends-example.md#redis--token-store-distributed` for implementation.

### If the app monitors health (production systems)

Use health checks from `SensitiveFlow.HealthChecks`.

Rules:

- Check audit database health at startup and periodically.
- Check token store (Redis/DB) health.
- Fail fast if infrastructure is unavailable.
- See `docs/policies-discovery-health.md#health-checks` for health check patterns.

### If the app needs diagnostics or observability

Use OpenTelemetry integration from `SensitiveFlow.Diagnostics`.

Rules:

- Emit audit events as spans and metrics.
- Track pseudonymization operations.
- Monitor retention execution.
- See `docs/diagnostics.md` for ActivitySource and Meter setup.

## Implementation checklist

- Add package references only for needed packages.
- Annotate domain model fields deliberately.
- Register durable stores before decorators.
- Decorator order matters:
  - retry close to durable store
  - diagnostics wherever measurement should happen
  - buffered audit only when explicitly accepted
- Use async APIs on request paths.
- Add TestKit contract tests for custom stores.
- Add redaction leak tests for API responses/log payloads.
- Add performance tests using `SensitiveDataAssert` to ensure no silent regressions.
- Update docs when adding a new integration path.
- **For production systems:** plan for infrastructure monitoring (audit DB health, Redis availability).

## Common mistakes to avoid

See `docs/common-pitfalls.md` for 12 documented pitfalls. Key rules:

- Do not use in-memory token/audit stores in production.
- Do not call sync `SaveChanges` in ASP.NET Core request paths.
- Do not describe masking as anonymization.
- Do not store raw IP addresses in audit records.
- Do not suppress analyzer findings without a documented reason.
- Do not add buffered audit as a default until the app accepts possible in-memory loss.
- **Do not assume <2% overhead without testing your infrastructure.** Overhead is highly dependent on database/Redis latency.
- **Do not enable all features simultaneously** without understanding the cumulative overhead (audit + JSON masking + logging redaction can add up).

## Troubleshooting

For common issues:
- Check `docs/troubleshooting.md` first (e.g., "Audit not being written", "Token store timeouts")
- Verify database/Redis connectivity and latency
- Enable diagnostics (`docs/diagnostics.md`) to see actual traces
- Review `docs/database-providers.md` for provider-specific setup (SQLite/SQL Server/Postgres)

## Preferred code patterns

### Entity annotation

```csharp
public sealed class Customer
{
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;
}
```

See `docs/attributes.md` for all attribute options.

### DTO pattern for API responses

Design output DTOs that include only fields that should be visible:

```csharp
public sealed class CustomerDto
{
    public string Id { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    // Internal fields are never included in DTO
}
```

See `docs/dto-pattern.md` for complete patterns and trade-offs.

### Store contract test

```csharp
public sealed class SqlTokenStoreTests : TokenStoreContractTests
{
    protected override Task<ITokenStore> CreateStoreAsync()
        => Task.FromResult<ITokenStore>(new SqlTokenStore(...));
}
```

See `docs/testkit.md` for all contract test types.

### Redaction assertion

```csharp
var payload = JsonSerializer.Serialize(customer, options);
SensitiveDataAssert.DoesNotLeak(payload, customer);
```

### Audit event publishing with outbox pattern

For guaranteed audit delivery even if database is slow:

```csharp
builder.Services.AddAuditEFCoreOutbox();
builder.Services.AddOutboxPublisher();
```

See `docs/outbox-example.md` for complete outbox setup and trade-offs.

### Custom token store backends

Implement `ITokenStore` for alternative persistence (e.g., MongoDB, DynamoDB):

```csharp
public sealed class CustomTokenStore : ITokenStore
{
    public async Task<string> GetOrCreateTokenAsync(string originalValue)
        => // your implementation
}
```

Add contract tests and see `docs/backends-example.md` for complete examples.

## Review checklist for AI agents

When reviewing a SensitiveFlow change, check:

- Are annotated values protected at every output boundary?
- Does audit use a durable store?
- Are audit writes batched or retried where needed?
- Is the token store durable and concurrency-safe?
- Are sync-over-async paths avoided in ASP.NET Core?
- Are provider-specific persistence paths tested?
- Are docs and examples using real public APIs?
- **Performance:** Have benchmarks been updated if interceptors/decorators were added?
- **Infrastructure:** Is the code prepared for slow database/Redis scenarios (with appropriate logging/health checks)?
- **Cumulative overhead:** Does the combination of enabled features stay under acceptable thresholds for the target environment?
