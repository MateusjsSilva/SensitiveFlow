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

### 1. Install packages

```bash
dotnet add package SensitiveFlow.Core
dotnet add package SensitiveFlow.Audit
dotnet add package SensitiveFlow.Audit.EFCore
dotnet add package SensitiveFlow.EFCore
dotnet add package SensitiveFlow.AspNetCore
```

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

### 3. Register a durable audit store

`IAuditStore` is the persistence contract -- you own the implementation so audit records go
exactly where your infrastructure requires (SQL, MongoDB, Azure Table Storage, etc.).

For EF Core-backed audit storage, use `SensitiveFlow.Audit.EFCore`. It registers an
`IAuditStore` that also implements `IBatchAuditStore`, avoiding one database roundtrip
per sensitive field.

```csharp
builder.Services.AddEfCoreAuditStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuditStorage")));
builder.Services.AddAuditStoreRetry();
```

> **Do not use an in-memory store in production.** Audit records must survive process
> restarts. Losing audit history defeats the accountability the audit trail is meant
> to provide.

### 4. Register services

```csharp
builder.Services.AddEfCoreAuditStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuditStorage")));
builder.Services.AddAuditStoreRetry();
builder.Services.AddSensitiveFlowEFCore();           // SensitiveFlow.EFCore
builder.Services.AddSensitiveFlowAspNetCore();       // SensitiveFlow.AspNetCore
builder.Services.AddSensitiveFlowLogging();          // SensitiveFlow.Logging
builder.Services.AddSensitiveFlowValidation(o =>
{
    o.RequireAuditStore = true;
    o.RequireTokenStore = true;
});
```

### 5. Add the middleware

```csharp
// Before UseAuthentication -- makes the pseudonymized IP token available to all handlers.
app.UseSensitiveFlowAudit();
```

### 6. Wire the interceptor into your DbContext

```csharp
optionsBuilder.AddInterceptors(app.Services.GetRequiredService<SensitiveDataAuditInterceptor>());
```

### Optional: policies, reports, and health checks

```csharp
var options = SensitiveFlowPolicyConfiguration.Create(options =>
{
    options.UseProfile(SensitiveFlowProfile.Strict);
    options.Policies.ForCategory(DataCategory.Contact)
        .MaskInLogs()
        .RedactInJson()
        .AuditOnChange();
});

var report = SensitiveDataDiscovery.Scan(typeof(Customer).Assembly);
File.WriteAllText("sensitiveflow-report.md", report.ToMarkdown());

builder.Services.AddSensitiveFlowHealthChecks()
    .AddAuditStoreCheck()
    .AddTokenStoreCheck();

var startupReport = app.Services.ValidateSensitiveFlow();
```

Defaults:

- profile: `SensitiveFlowProfile.Balanced`
- JSON redaction: `JsonRedactionMode.Mask`
- redaction marker: `[REDACTED]`
- logging: `[Sensitive]` markers are redacted; annotated structured object members are redacted unless policies request `MaskInLogs()`
- retention anonymization marker: `[ANONYMIZED]`
- health checks: `sensitiveflow-audit-store`, `sensitiveflow-token-store`

Every `SaveChanges` on a field annotated with `[PersonalData]` or `[SensitiveData]` now
produces an `AuditRecord` automatically.

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
