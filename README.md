# SensitiveFlow

<p align="center">
  <img src="assets/logo.png" alt="SensitiveFlow logo" width="200" />
</p>

[![CI](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/ci.yml)
[![Container Tests](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/container-tests.yml/badge.svg)](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/container-tests.yml)
[![CodeQL](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/codeql.yml/badge.svg)](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/SensitiveFlow.Core?label=nuget)](https://www.nuget.org/packages/SensitiveFlow.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SensitiveFlow.Core)](https://www.nuget.org/packages/SensitiveFlow.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-blue)](https://dotnet.microsoft.com)

**SensitiveFlow** is a .NET library that brings observability and control to sensitive data. It focuses on runtime behavior -- automatic auditing, log redaction, and masking -- not compliance paperwork.

> **Important:** SensitiveFlow helps reduce accidental exposure of sensitive data, but it does not guarantee legal compliance or complete data protection by itself. You are responsible for how you use these primitives in your application.

## Why SensitiveFlow?

Sensitive data flows through your application on every request: EF Core saves, HTTP responses, log lines. SensitiveFlow makes that flow visible and controlled at the infrastructure level, without requiring manual instrumentation.

## Packages

| Package | Description | Status |
|---------|-------------|--------|
| `SensitiveFlow.Core` | Attributes, enums, interfaces, models, exceptions | Preview |
| `SensitiveFlow.Audit` | Immutable audit trail -- bring your own durable store; retry and buffered decorators included | Preview |
| `SensitiveFlow.Audit.EFCore` | Durable EF Core-backed audit store (`IAuditStore` + `IBatchAuditStore`) | Preview |
| `SensitiveFlow.Audit.Snapshots.EFCore` | Durable EF Core-backed aggregate snapshot store (`IAuditSnapshotStore`) | Preview |
| `SensitiveFlow.TokenStore.EFCore` | Durable EF Core-backed token store for reversible pseudonymization (`ITokenStore` + `IPseudonymizer`) | Preview |
| `SensitiveFlow.EFCore` | SaveChanges interceptor for automatic auditing | Preview |
| `SensitiveFlow.AspNetCore` | Middleware for actor/IP context enrichment | Preview |
| `SensitiveFlow.Logging` | ILogger decorator for PII redaction in logs | Preview |
| `SensitiveFlow.Diagnostics` | OpenTelemetry bridge (ActivitySource + Meter) for audit/logging spans & metrics | Preview |
| `SensitiveFlow.HealthChecks` | Microsoft health checks for audit/token infrastructure | Preview |
| `SensitiveFlow.Anonymization` | Masking, anonymization, pseudonymization, erasure, data export, and deterministic fingerprints | Preview |
| `SensitiveFlow.Json` | `System.Text.Json` modifier that masks/redacts/omits annotated properties at serialization time | Preview |
| `SensitiveFlow.Retention` | Retention metadata and expiration hook contracts | Preview |
| `SensitiveFlow.Analyzers` | Roslyn analyzers for privacy anti-patterns | Preview |
| `SensitiveFlow.Analyzers.CodeFixes` | Quick-fix providers for SF0001/SF0002 (wrap with `.MaskEmail()` / `.MaskPhone()` / `.MaskName()`) | Preview |
| `SensitiveFlow.SourceGenerators` | Source generator that precomputes sensitive/retention member metadata | Preview |
| `SensitiveFlow.TestKit` | xUnit conformance bases for `IAuditStore` / `ITokenStore` plus a `SensitiveDataAssert` leak-detection helper | Preview |
| `SensitiveFlow.Tool` | `dotnet tool` command for discovery reports from annotated assemblies | Preview |

## Quick Start

### 1. Install the composition package

```bash
dotnet add package SensitiveFlow.AspNetCore.EFCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer # or your EF Core provider
```

This single package brings in the full recommended stack: Core, Audit, EF Core audit
stores and outbox, Token store, Anonymization, JSON and logging redaction, Retention,
Diagnostics, and Health Checks. Database provider packages stay app-owned, so the
same setup works with SQL Server, PostgreSQL, SQLite, MySQL, or any EF Core provider.

### 2. Annotate your model

```csharp
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public class Customer
{
    public Guid Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Other)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;
}
```

`DataSubjectId` or `UserId` is required for EF Core audit correlation.

### 3. Register SensitiveFlow

```csharp
using SensitiveFlow.AspNetCore.EFCore.Extensions;

builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);

    // Provider-agnostic: use any EF Core provider referenced by your app.
    options.UseEfCoreStores(
        audit => audit.UseSqlServer(builder.Configuration.GetConnectionString("Audit")!),
        tokens => tokens.UseSqlServer(builder.Configuration.GetConnectionString("Tokens")!));

    // You can also configure each store independently:
    // options.UseEfCoreAuditStore(audit => audit.UseNpgsql(...));
    // options.UseEfCoreTokenStore(tokens => tokens.UseSqlite(...));

    // Enable the features your app needs
    options.EnableEfCoreAudit();
    options.EnableAspNetCoreContext();
    options.EnableJsonRedaction();
    options.EnableLoggingRedaction();
    options.EnableValidation();
    options.EnableHealthChecks();

    // Production-grade features (opt-in)
    // options.EnableOutbox();
    // options.EnableDiagnostics();
    // options.EnableAuditStoreRetry();
    // options.EnableCachingTokenStore();
    // options.EnableRetention().EnableRetentionExecutor();
    // options.EnableDataSubjectExport().EnableDataSubjectErasure();
});
```

### 4. Wire your DbContext and middleware

```csharp
// DbContext setup
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
        .AddInterceptors(sp.GetRequiredService<SensitiveDataAuditInterceptor>()));

var app = builder.Build();

// Place before UseAuthentication so the pseudonymized IP token
// is available throughout the request pipeline.
app.UseSensitiveFlow();

// Map health checks (registers under /health by default)
app.MapHealthChecks("/health");
```

That's it. Every `SaveChanges` on a field annotated with `[PersonalData]` or
`[SensitiveData]` now produces an `AuditRecord` automatically, HTTP responses are
JSON-redacted, logs are scrubbed, and health checks monitor the infrastructure.

Schema is explicit: databases must already contain the app tables and the
SensitiveFlow audit/token/outbox tables before the first write. SensitiveFlow does
not create tables automatically, even in samples, because schema creation belongs
to migrations or deployment tooling.

### Optional: policies, reports, and diagnostics

```csharp
// Custom policy overrides
builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Strict);
    // ... store config, features ...
});

// Discovery report (CLI or at startup)
var report = SensitiveDataDiscovery.Scan(typeof(Customer).Assembly);
File.WriteAllText("sensitiveflow-report.md", report.ToMarkdown());

// Startup validation
var startupReport = app.Services.ValidateSensitiveFlow();
```

### Defaults

| Area | Default |
|------|---------|
| Profile | `SensitiveFlowProfile.Balanced` |
| JSON redaction mode | `JsonRedactionMode.Mask` |
| Redaction marker | `[REDACTED]` |
| Logging | `[Sensitive]` markers redacted; annotated members redacted unless `MaskInLogs()` |
| Retention anonymization | `[ANONYMIZED]` |
| Audit outbox (when enabled) | `PollInterval=1s`, `BatchSize=100`, `MaxAttempts=5`, `BackoffStrategy=Exponential` |
| Data-subject export | Raw annotated values by default; use `[Redaction(Export = ...)]` to override |
| Health checks | `sensitiveflow-audit-store`, `sensitiveflow-token-store`, `sensitiveflow-audit-outbox` |

### Advanced: fine-grained package-by-package setup

The composition layer is the recommended path. If you need precise control over every
registration, install individual packages and register each service manually. See the
[Advanced Composition](docs/package-reference.md) guide for the full package-by-package
setup matrix and individual registration calls.

> **Do not use in-memory stores in production.** Audit records and token mappings must
> survive process restarts. Losing audit history defeats accountability; losing token
> mappings makes pseudonymized data irrecoverable.

## Documentation

- [Documentation index](docs/README.md)
- [Getting Started](docs/getting-started.md)
- [Package reference](docs/package-reference.md)
- [AI skill guide](docs/ai-skill-sensitiveflow.md)

## Design Principles

- **Runtime behavior over compliance paperwork** -- instruments what actually happens, not what should happen.
- **Explicit metadata over implicit heuristics** -- every classification is opt-in via attributes.
- **Composition over lock-in** -- each module is optional and independently testable.
- **Safe defaults** -- the IP address is never stored raw; the log redactor strips sensitive values before they reach any sink.
- **Bring your own persistence** -- `IAuditStore` and `ITokenStore` are contracts, not implementations. You choose the database.

## License

MIT
