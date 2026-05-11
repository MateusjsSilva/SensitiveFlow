# Package reference

This document summarizes each SensitiveFlow package individually: purpose, primary APIs, dependencies, setup, and operational notes.

## Setup matrix

| Package | Use when | Minimum setup | Main risk |
| --- | --- | --- | --- |
| `SensitiveFlow.AspNetCore.EFCore` | **Recommended first package.** You're building an ASP.NET Core + EF Core app and want one composition entry point. | `builder.Services.AddSensitiveFlowWeb(options => { ... })`. | Hides individual registrations; switch to per-package setup when you need full control. |
| `SensitiveFlow.Core` | You annotate models or implement contracts. | Add attributes to models. | None by itself; it does not enforce behavior. |
| `SensitiveFlow.Audit` | You register custom audit stores or decorators. | `AddAuditStore<T>()` or first-party EF store plus optional retry/buffer. | In-memory/buffered data can be lost before durable write. |
| `SensitiveFlow.Audit.EFCore` | You want first-party SQL audit storage. | `AddEfCoreAuditStore(...)`; create/migrate audit table. | Audit DB must be durable and backed up independently. |
| `SensitiveFlow.Audit.EFCore.Outbox` | You need durable, reliable delivery of audit records to downstream systems (SIEM, data lakes, compliance dashboards). | `AddEfCoreAuditOutbox()` and register `IAuditOutboxPublisher` implementations. | Outbox table must be monitored for dead-lettered entries (failed delivery after max retries). |
| `SensitiveFlow.Audit.Snapshots.EFCore` | You want durable aggregate-level audit snapshots. | `AddEfCoreAuditSnapshotStore(...)`; create/migrate snapshot table. | Snapshots can be large; monitor storage growth. |
| `SensitiveFlow.TokenStore.EFCore` | You want first-party SQL token storage for reversible pseudonymization. | `AddEfCoreTokenStore(...)`; create/migrate token table. | Losing token mappings makes pseudonymized data irrecoverable. |
| `SensitiveFlow.EFCore` | You want automatic audit on `SaveChanges`. | `AddSensitiveFlowEFCore()` and `AddInterceptors(...)`. | Missing interceptor means no automatic audit. |
| `SensitiveFlow.AspNetCore` | You need actor/IP context from HTTP requests. | `AddSensitiveFlowAspNetCore()` and `UseSensitiveFlowAudit()`. | Requires a durable `IPseudonymizer`/`ITokenStore` for reversible IP tokens. |
| `SensitiveFlow.Anonymization` | You mask, pseudonymize, export, erase, or fingerprint values. | Register an `ITokenStore` for reversible tokens; use services/extensions. | Masking/pseudonymization are still personal data. |
| `SensitiveFlow.Json` | You need automatic response serialization redaction. | `WithSensitiveDataRedaction()` on `System.Text.Json`. | Does not cover Newtonsoft.Json. |
| `SensitiveFlow.Logging` | You need sensitive log value redaction. | `AddSensitiveFlowLogging()` and/or provider wrapper. | Not semantic PII detection; scalar values still need explicit markers or structured metadata. |
| `SensitiveFlow.Diagnostics` | You want OpenTelemetry spans/metrics. | `AddSensitiveFlowDiagnostics()` after audit store/decorators. | Decorator order changes what latency is measured. |
| `SensitiveFlow.HealthChecks` | You want ASP.NET Core health checks for SensitiveFlow infrastructure. | `AddSensitiveFlowHealthChecks().AddAuditStoreCheck().AddTokenStoreCheck().AddAuditOutboxCheck()`. | Token stores without `IHealthProbe` are resolution-only checks to avoid mutating data; in-memory audit outbox is `Degraded` outside Development. |
| `SensitiveFlow.Retention` | You evaluate retention policies. | `AddRetention()` / `AddRetentionExecutor()` and run a scheduled job. | It will not delete database rows automatically. |
| `SensitiveFlow.Analyzers` | You want compile-time guardrails. | Add analyzer package to application projects. | Warnings still require engineering judgment. |
| `SensitiveFlow.SourceGenerators` | You want generated sensitive metadata. | Add source generator package. | Keep generator tests aligned with reflection fallback. |
| `SensitiveFlow.TestKit` | You implement custom stores or leak tests. | Inherit contract tests. | Contract tests need isolated fresh stores. |
| `SensitiveFlow.Tool` | You want CI/documentation reports from annotated assemblies. | `dotnet tool install SensitiveFlow.Tool`; run `sensitiveflow scan <assembly-project-or-directory>`. | Project/source inputs are built first, then compiled assemblies are scanned. |

## SensitiveFlow.AspNetCore.EFCore

Purpose:

- **Recommended first package.** Official high-level composition for ASP.NET Core + EF Core apps. Provides a single `AddSensitiveFlowWeb()` entry point that wires the recommended stack: audit, token store, outbox, JSON and logging redaction, EF Core interception, ASP.NET Core context, validation, diagnostics, health checks, anonymization, and retention.

Primary APIs:

- `AddSensitiveFlowWeb(options => { ... })`
- `UseSensitiveFlow()` (middleware — wraps `UseSensitiveFlowAudit()`)
- `SensitiveFlowWebOptions` (fluent builder)
  - `UseProfile(SensitiveFlowProfile profile)`
  - `UseEfCoreStores(configureAuditStore, configureTokenStore)` (provider-agnostic shorthand)
  - `UseEfCoreAuditStore(Action<DbContextOptionsBuilder>)`
  - `UseEfCoreTokenStore(Action<DbContextOptionsBuilder>)`
  - `EnableOutbox()`, `EnableDiagnostics()`, `EnableAuditStoreRetry()`
  - `EnableCachingTokenStore()`
  - `EnableDataSubjectExport()`, `EnableDataSubjectErasure()`
  - `EnableLoggingRedaction()`, `EnableJsonRedaction()`
  - `EnableEfCoreAudit()`, `EnableAspNetCoreContext()`
  - `EnableValidation()`, `EnableHealthChecks()`
  - `EnableRetention()`, `EnableRetentionExecutor()`

Install when:

- You're building an ASP.NET Core + EF Core app and want a single-line setup.
- You're onboarding to SensitiveFlow and want sensible defaults without reading every package's setup docs.

Recommended setup:

```csharp
builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);
    options.UseEfCoreStores(
        audit => audit.UseSqlServer(builder.Configuration.GetConnectionString("Audit")!),
        tokens => tokens.UseSqlServer(builder.Configuration.GetConnectionString("Tokens")!));
    options.EnableEfCoreAudit();
    options.EnableAspNetCoreContext();
    options.EnableJsonRedaction();
    options.EnableLoggingRedaction();
    options.EnableValidation();
    options.EnableHealthChecks();
});

app.UseSensitiveFlow();
app.MapHealthChecks("/health");
```

Operational notes:

- This package depends on all other SensitiveFlow packages. Database provider packages remain app-owned, so the same API works with SQL Server, PostgreSQL, SQLite, MySQL, or any EF Core provider.
- Every `Enable*()` method maps to the corresponding granular extension method (`AddEfCoreAuditStore`, `AddSensitiveFlowEFCore`, etc.).
- SensitiveFlow does not create database tables automatically. If the audit/token/outbox tables do not exist, EF Core persistence will fail on first write. That is intentional: schema creation is an app/deployment responsibility.
- Use migrations, checked-in SQL scripts, or your deployment tooling to create the app tables and SensitiveFlow infrastructure tables before startup.
- When you outgrow the composition layer, switch to per-package setup for full control.
- Health checks automatically include `AddAuditOutboxCheck()` when outbox is enabled.

## SensitiveFlow.Core

Purpose:

- Shared contracts and metadata used by every other package.

Primary APIs:

- Attributes: `[PersonalData]`, `[SensitiveData]`, `[RetentionData]`.
- Output attributes: `[Redact]`, `[Mask]`, `[Omit]`, `[Redaction]`.
- Models: `AuditRecord`, `AuditSnapshot`.
- Contracts: `IAuditStore`, `IBatchAuditStore`, `IAuditSnapshotStore`, `ITokenStore`, `IPseudonymizer`, `IMasker`, `IAnonymizer`, `IAuditContext`, data-subject request interfaces, audit outbox interfaces.
- Enums: `DataCategory`, `SensitiveDataCategory`, `DataSensitivity`, `RetentionPolicy`, `AuditOperation`, `AnonymizationType`.
- Policies/profiles: `SensitiveFlowOptions`, `SensitiveFlowProfile`, `SensitiveFlowPolicyRegistry`.
- Discovery/export: `SensitiveDataDiscovery`, `JsonDataExportFormatter`, `CsvDataExportFormatter`.
- Defaults: `SensitiveFlowDefaults` documents the default profile, redaction marker, anonymization marker, and health-check names.
- Cache: `SensitiveMemberCache`.

Install when:

- Any project needs to annotate models or implement SensitiveFlow contracts.

Operational notes:

- Keep this package dependency-light.
- `SensitiveMemberCache` is per process and grows by application type count, not traffic volume.

## SensitiveFlow.Audit

Purpose:

- Audit-store registration and audit decorators.

Primary APIs:

- `AddAuditStore<TStore>()`
- `AddAuditStoreRetry(...)`
- `AddBufferedAuditStore(...)`
- `AddAuditOutbox<TOutbox>()`
- `AddInMemoryAuditOutbox()`
- `RetryingAuditStore`
- `BufferedAuditStore`
- `OutboxAuditStore`
- `JsonAuditOutboxSerializer`
- `InMemoryAuditSnapshotStore`

Install when:

- You need to register a custom durable `IAuditStore`.
- You want retry or buffered append behavior.
- You use aggregate audit snapshots.

Recommended setup:

```csharp
builder.Services.AddAuditStore<MyDurableAuditStore>();
builder.Services.AddAuditStoreRetry();
```

Operational notes:

- `RetryingAuditStore` does not swallow exhausted failures.
- `BufferedAuditStore` is advanced. It can lose records on process crash before flush. Avoid using it with scoped stores until lifetime semantics are hardened.
- `AddInMemoryAuditOutbox()` is obsolete for production and should only be used in tests/local development.
- Production outboxes should implement `IDurableAuditOutbox` and use `AddAuditOutbox<TOutbox>()` plus at least one `IAuditOutboxPublisher`.

## SensitiveFlow.Audit.EFCore

Purpose:

- First-party EF Core-backed durable audit store.

Primary APIs:

- `AddEfCoreAuditStore(options => ...)`
- `AddEfCoreAuditStore<TContext>()`
- `EfCoreAuditStore<TContext>`
- `AuditDbContext`
- `AuditRecordEntityTypeConfiguration`
- `IAuditLogRetention`
- `AuditLogRetention<TContext>`

Install when:

- You want audit records persisted through EF Core.

Recommended setup:

```csharp
builder.Services.AddEfCoreAuditStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Audit")));
builder.Services.AddAuditStoreRetry();
```

Operational notes:

- Uses `IDbContextFactory<TContext>` so audit writes do not piggyback on the application `DbContext`.
- Implements `IBatchAuditStore`.
- PostgreSQL and SQL Server container coverage lives in `tests/SensitiveFlow.Audit.EFCore.ContainerTests`.

## SensitiveFlow.Audit.EFCore.Outbox

Purpose:

- First-party EF Core-backed **durable audit outbox** for reliable, transactional delivery of audit records to downstream systems (SIEM, data lakes, compliance dashboards, Kafka, webhooks, etc.).

Primary APIs:

- `AddEfCoreAuditOutbox(options => ...)`
- `EfCoreAuditOutbox`
- `AuditOutboxEntry` (data model in Core)
- `IAuditOutboxPublisher` (interface for delivery logic — implemented by you)
- `AuditOutboxDispatcher` (background service that polls and delivers)
- `AuditOutboxDispatcherOptions` (configurable polling, retry backoff, max attempts)

Install when:

- You need **guaranteed, at-least-once delivery** of audit records to a remote system.
- Your business/compliance requirements demand that no audit record is lost if the application crashes.

Recommended setup:

```csharp
// Package installation
dotnet add package SensitiveFlow.Audit.EFCore.Outbox

// DI setup
builder.Services.AddEfCoreAuditStore(opt => 
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Audit")));

// Enable durable outbox with automatic background dispatcher
builder.Services.AddEfCoreAuditOutbox(options =>
{
    options.PollInterval = TimeSpan.FromSeconds(2);
    options.BatchSize = 100;
    options.MaxAttempts = 5;
    options.BackoffStrategy = BackoffStrategy.Exponential; // or Linear
});

// Register publishers (one or more) to deliver records downstream
builder.Services.AddScoped<IAuditOutboxPublisher, MySiemPublisher>();
builder.Services.AddScoped<IAuditOutboxPublisher, MyDataLakePublisher>();
```

Example publisher implementation:

```csharp
public sealed class MySiemPublisher : IAuditOutboxPublisher
{
    private readonly HttpClient _http;
    
    public MySiemPublisher(HttpClient http) => _http = http;
    
    public async Task PublishAsync(AuditOutboxEntry entry, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(entry.Record);
        var response = await _http.PostAsJsonAsync("/siem/audit", json, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
```

Delivery guarantee:

- Records are enqueued and the audit store write happen in a **single `SaveChanges` transaction** — transactional outbox pattern.
- The `AuditOutboxDispatcher` background service polls periodically and calls all registered `IAuditOutboxPublisher` implementations.
- On successful publish, entries are marked `IsProcessed`.
- On exception, entries are marked `IsDeadLettered` or retried based on `MaxAttempts`.
- Dead-lettered entries and retry history are queryable for operational dashboards and alerting.

Defaults:

| Option | Default |
| --- | --- |
| `PollInterval` | `1s` |
| `BatchSize` | `100` |
| `MaxAttempts` | `5` |
| `BackoffStrategy` | `Exponential` |
| `DeadLetterAfterMax` | `true` |

Operational notes:

- Audit and outbox data live in the same `AuditDbContext` (configurable persistence strategy).
- Dispatcher runs as a `HostedService` — activate after application startup.
- Multiple instances of your application can run simultaneously, but you should validate duplicate-delivery tolerance because outbox delivery is at-least-once.
- Monitor the `AuditOutboxEntry` table for growth, dead-lettered entries, and retry counts in operational dashboards.
- SQLite coverage lives in `tests/SensitiveFlow.Audit.EFCore.Outbox.Tests`; provider-specific container coverage should be added when SQL Server/PostgreSQL migrations are introduced.

## SensitiveFlow.Audit.Snapshots.EFCore

Purpose:

- First-party EF Core-backed durable aggregate audit snapshot store.

Primary APIs:

- `AddEfCoreAuditSnapshotStore(options => ...)`
- `AddEfCoreAuditSnapshotStore<TContext>()`
- `EfCoreAuditSnapshotStore<TContext>`
- `SnapshotDbContext`
- `AuditSnapshotEntityTypeConfiguration`

Install when:

- You want aggregate-level audit snapshots persisted through EF Core (instead of per-field `AuditRecord`).

Recommended setup:

```csharp
builder.Services.AddEfCoreAuditSnapshotStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Snapshots")));
```

Operational notes:

- Uses `IDbContextFactory<TContext>` so snapshot writes do not piggyback on the application `DbContext`.
- Snapshots carry serialized "before" and "after" JSON — monitor storage growth.
- Indexes optimized for aggregate lookups (`Aggregate + AggregateId + Timestamp`) and data-subject queries.

## SensitiveFlow.TokenStore.EFCore

Purpose:

- First-party EF Core-backed durable token store for reversible pseudonymization.

Primary APIs:

- `AddEfCoreTokenStore(options => ...)`
- `AddEfCoreTokenStore<TContext>()`
- `EfCoreTokenStore<TContext>`
- `TokenDbContext`
- `TokenMappingEntityTypeConfiguration`

Install when:

- You want `TokenPseudonymizer` backed by a durable SQL store without writing your own `ITokenStore`.

Recommended setup:

```csharp
builder.Services.AddEfCoreTokenStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Tokens")));
```

Operational notes:

- Registers both `ITokenStore` (Singleton) and `IPseudonymizer` (Scoped, via `TokenPseudonymizer`).
- Unique index on `Value` enables concurrency-safe `GetOrCreateTokenAsync` — concurrent callers racing for the same value recover via `DbUpdateException` catch and read the winner's token.
- **Critical:** losing token mappings makes previously pseudonymized data irrecoverable. Back up the token database independently.

## SensitiveFlow.EFCore

Purpose:

- Automatically emits audit records from EF Core `SaveChanges`.

Primary APIs:

- `AddSensitiveFlowEFCore()`
- `SensitiveDataAuditInterceptor`
- `NullAuditContext`

Install when:

- You want automatic audit records for EF entities with `[PersonalData]` or `[SensitiveData]`.

Recommended setup:

```csharp
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddScoped<IAuditContext, MyAuditContext>();
```

Operational notes:

- Prefer `SaveChangesAsync`.
- Sync `SaveChanges` blocks on async flush and is not recommended for ASP.NET Core.
- Entities must expose `DataSubjectId` or `UserId`.

## SensitiveFlow.AspNetCore

Purpose:

- HTTP request integration for actor/IP audit context.

Primary APIs:

- `AddSensitiveFlowAspNetCore()`
- `UseSensitiveFlowAudit()`
- `HttpAuditContext`
- `SensitiveFlowAuditMiddleware`

Install when:

- You need request-derived `IAuditContext` values.

Recommended setup:

```csharp
builder.Services.AddSensitiveFlowAspNetCore();
app.UseSensitiveFlowAudit();
```

Operational notes:

- Requires an `IPseudonymizer` for IP token generation.
- The pseudonymizer must use a durable token store in production.

## SensitiveFlow.Anonymization

Purpose:

- Data protection primitives: masking, anonymization, pseudonymization, erasure, export, and fingerprints.

Primary APIs:

- `BrazilianTaxIdAnonymizer`
- `EmailMasker`, `PhoneMasker`, `NameMasker`
- `TokenPseudonymizer`, `HmacPseudonymizer`
- `CachingTokenStore`
- `DeterministicFingerprint`
- `DataSubjectErasureService`
- `DataSubjectExporter`
- String extensions such as `MaskEmail()`, `MaskPhone()`, `MaskName()`, `PseudonymizeHmac(...)`
- DI: `AddTokenStore<TStore>()`, `AddCachingTokenStore(...)`, `AddDataSubjectErasure()`, `AddDataSubjectExport()`

Install when:

- You need to reduce exposure of personal data in UI, logs, audit fields, exports, or comparison workflows.

Operational notes:

- Only anonymization may remove data from personal-data scope. Masking and pseudonymization remain personal data.
- `TokenPseudonymizer` is reversible only if `ITokenStore` remains durable.
- `CachingTokenStore` stores original values in process memory.
- `DataSubjectExporter` returns raw values by default and only masks/redacts/omits fields that opt in with `[Redaction(Export = ...)]`.

## SensitiveFlow.Json

Purpose:

- Redact annotated properties during `System.Text.Json` serialization.

Primary APIs:

- `WithSensitiveDataRedaction(...)`
- `AddSensitiveFlowJsonRedaction(...)`
- `JsonRedactionOptions`
- `[JsonRedaction]`
- `JsonRedactionMode`

Install when:

- API responses or payload serialization must avoid leaking annotated values.

Recommended setup:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WithSensitiveDataRedaction();
});
```

Operational notes:

- Uses `System.Text.Json` metadata modifiers.
- Validate payloads with `SensitiveDataAssert.DoesNotLeak(...)` in tests.

## SensitiveFlow.Logging

Purpose:

- Redact sensitive values before they reach an `ILoggerProvider`.

Primary APIs:

- `AddSensitiveFlowLogging(...)`
- `AddSensitiveFlowLogging<TProvider>(...)`
- `RedactingLoggerProvider`
- `SensitiveLoggingOptions`
- `ISensitiveValueRedactor`
- `DefaultSensitiveValueRedactor`

Install when:

- You want a logging provider wrapper that replaces known sensitive values with a marker.

Operational notes:

- This is not full semantic PII detection.
- `[Sensitive]` template markers are always redacted.
- Structured object values with `[PersonalData]` or `[SensitiveData]` members are redacted by default.
- Pass `SensitiveLoggingOptions.Policies` to make `.MaskInLogs()` category policies mask annotated structured object members.
- Prefer structured logging and pass scalar values through known redaction paths.

## SensitiveFlow.Diagnostics

Purpose:

- OpenTelemetry-friendly tracing and metrics for audit appends.

Primary APIs:

- `AddSensitiveFlowDiagnostics()`
- `InstrumentedAuditStore`
- `SensitiveFlowDiagnostics`
- `SensitiveFlowInstruments`

Install when:

- You want spans and metrics around audit append latency/count.

Recommended setup:

```csharp
builder.Services.AddEfCoreAuditStore(...);
builder.Services.AddAuditStoreRetry();
builder.Services.AddSensitiveFlowDiagnostics();
```

Operational notes:

- Decorator order changes what the span measures.
- Query operations are intentionally not instrumented.

## SensitiveFlow.Retention

Purpose:

- Evaluate and execute retention policies declared with `[RetentionData]`.

Primary APIs:

- `AddRetention()`
- `AddRetentionHandler<THandler>()`
- `AddRetentionExecutor(...)`
- `RetentionEvaluator`
- `RetentionExecutor`
- `RetentionExecutionReport`
- `IRetentionExpirationHandler`

Install when:

- You need scheduled lifecycle evaluation for personal data fields.

Operational notes:

- The library does not automatically delete database rows.
- `RetentionExecutor` mutates fields for anonymization and reports delete/block/notify actions for application code.

## SensitiveFlow.Analyzers

Purpose:

- Compile-time guardrails for privacy anti-patterns.

Primary diagnostics:

- Sensitive data logged directly.
- Sensitive data returned directly.
- Missing `DataSubjectId` on auditable entities.

Install when:

- You want CI/build-time warnings for common privacy mistakes.

Operational notes:

- Treat analyzer findings as design prompts. Do not suppress without documenting why.

## SensitiveFlow.Analyzers.CodeFixes

Purpose:

- Code fixes for analyzer findings.

Primary API:

- `WrapWithMaskCodeFixProvider`

Install when:

- Developer IDEs should offer quick fixes for masking common string values.

Operational notes:

- Code fixes should remain conservative. They cannot infer domain-specific privacy policy.

## SensitiveFlow.SourceGenerators

Purpose:

- Generate sensitive/retention metadata to reduce runtime reflection.

Primary API:

- `SensitiveMemberGenerator`

Install when:

- You want compile-time metadata for annotated model members.

Operational notes:

- Keep generated metadata registration wired through `SensitiveMemberCache`.
- Continue testing inheritance, partial types, and interface edge cases.

## SensitiveFlow.TestKit

Purpose:

- Reusable tests for custom SensitiveFlow integrations.

Primary APIs:

- `AuditStoreContractTests`
- `TokenStoreContractTests`
- `SensitiveDataAssert.DoesNotLeak(...)`

Install when:

- You implement an `IAuditStore` or `ITokenStore`.
- You want redaction leak assertions in application tests.

Recommended setup:

```csharp
public sealed class MyAuditStoreTests : AuditStoreContractTests
{
    protected override Task<IAuditStore> CreateStoreAsync()
        => Task.FromResult<IAuditStore>(new MyAuditStore(...));
}
```

Operational notes:

- Contract tests assume a fresh isolated store per test.
- For durable stores, create an isolated database/schema/table prefix per test.
