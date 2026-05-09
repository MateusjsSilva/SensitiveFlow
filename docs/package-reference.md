# Package reference

This document summarizes each SensitiveFlow package individually: purpose, primary APIs, dependencies, setup, and operational notes.

## Setup matrix

| Package | Use when | Minimum setup | Main risk |
| --- | --- | --- | --- |
| `SensitiveFlow.Core` | You annotate models or implement contracts. | Add attributes to models. | None by itself; it does not enforce behavior. |
| `SensitiveFlow.Audit` | You register custom audit stores or decorators. | `AddAuditStore<T>()` or first-party EF store plus optional retry/buffer. | In-memory/buffered data can be lost before durable write. |
| `SensitiveFlow.Audit.EFCore` | You want first-party SQL audit storage. | `AddEfCoreAuditStore(...)`; create/migrate audit table. | Audit DB must be durable and backed up independently. |
| `SensitiveFlow.EFCore` | You want automatic audit on `SaveChanges`. | `AddSensitiveFlowEFCore()` and `AddInterceptors(...)`. | Missing interceptor means no automatic audit. |
| `SensitiveFlow.AspNetCore` | You need actor/IP context from HTTP requests. | `AddSensitiveFlowAspNetCore()` and `UseSensitiveFlowAudit()`. | Requires a durable `IPseudonymizer`/`ITokenStore` for reversible IP tokens. |
| `SensitiveFlow.Anonymization` | You mask, pseudonymize, export, erase, or fingerprint values. | Register an `ITokenStore` for reversible tokens; use services/extensions. | Masking/pseudonymization are still personal data. |
| `SensitiveFlow.Json` | You need automatic response serialization redaction. | `WithSensitiveDataRedaction()` on `System.Text.Json`. | Does not cover Newtonsoft.Json. |
| `SensitiveFlow.Logging` | You need sensitive log value redaction. | `AddSensitiveFlowLogging()` and/or provider wrapper. | Not semantic PII detection; values must flow through known redaction paths. |
| `SensitiveFlow.Diagnostics` | You want OpenTelemetry spans/metrics. | `AddSensitiveFlowDiagnostics()` after audit store/decorators. | Decorator order changes what latency is measured. |
| `SensitiveFlow.Retention` | You evaluate retention policies. | `AddRetention()` / `AddRetentionExecutor()` and run a scheduled job. | It will not delete database rows automatically. |
| `SensitiveFlow.Analyzers` | You want compile-time guardrails. | Add analyzer package to application projects. | Warnings still require engineering judgment. |
| `SensitiveFlow.SourceGenerators` | You want generated sensitive metadata. | Add source generator package. | Keep generator tests aligned with reflection fallback. |
| `SensitiveFlow.TestKit` | You implement custom stores or leak tests. | Inherit contract tests. | Contract tests need isolated fresh stores. |

## SensitiveFlow.Core

Purpose:

- Shared contracts and metadata used by every other package.

Primary APIs:

- Attributes: `[PersonalData]`, `[SensitiveData]`, `[RetentionData]`.
- Models: `AuditRecord`, `AuditSnapshot`.
- Contracts: `IAuditStore`, `IBatchAuditStore`, `IAuditSnapshotStore`, `ITokenStore`, `IPseudonymizer`, `IMasker`, `IAnonymizer`, `IAuditContext`.
- Enums: `DataCategory`, `SensitiveDataCategory`, `RetentionPolicy`, `AuditOperation`, `AnonymizationType`.
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
- `RetryingAuditStore`
- `BufferedAuditStore`
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
- PostgreSQL container coverage lives in `tests/SensitiveFlow.Audit.EFCore.ContainerTests`.

## SensitiveFlow.EFCore

Purpose:

- Automatically emits audit records from EF Core `SaveChanges`.

Primary APIs:

- `AddSensitiveFlowEFCore()`
- `AddSensitiveFlowAuditContext<TContext>()`
- `SensitiveDataAuditInterceptor`
- `NullAuditContext`

Install when:

- You want automatic audit records for EF entities with `[PersonalData]` or `[SensitiveData]`.

Recommended setup:

```csharp
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowAuditContext<MyAuditContext>();
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
- `ISensitiveValueRedactor`
- `DefaultSensitiveValueRedactor`

Install when:

- You want a logging provider wrapper that replaces known sensitive values with a marker.

Operational notes:

- This is not full semantic PII detection.
- Prefer structured logging and pass values through known redaction paths.

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
