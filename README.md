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

**SensitiveFlow** is a .NET library that brings observability and control to sensitive data through automatic auditing, log redaction, JSON masking, and pseudonymization. Mark your data once, get automatic protection everywhere.

> **Important:** SensitiveFlow helps reduce accidental exposure of sensitive data, but it does not guarantee legal compliance or complete data protection by itself. You are responsible for how you use these primitives in your application.

## Why SensitiveFlow?

Sensitive data flows through your application constantly: EF Core database saves, HTTP JSON responses, structured logs, error messages. Without SensitiveFlow, that data is exposed everywhere—in logs, in responses, in backups, in error traces—often with no visibility into where it leaked or why.

SensitiveFlow gives you:

- **Automatic audit trail** — Every database change to a sensitive field is logged with who changed it, when, what changed, and why (from your request context). No manual instrumentation.
- **Log redaction** — Your structured logger scrubs sensitive values *before* they reach any sink. A developer who accidentally logs a customer's email will never see it in the logs; it's redacted automatically.
- **JSON response masking** — ASP.NET Core responses automatically mask sensitive fields in your JSON output without changing your serialization code.
- **Reversible pseudonymization** — Replace real values with tokens while keeping them queryable. Trade a customer's email for `token_abc123` in logs and analytics, then recover the original when needed.
- **Data retention & erasure** — Declare retention policies on fields; SensitiveFlow will anonymize or delete expired records on schedule.
- **Data subject export** — Generate privacy reports showing all data associated with a customer in your system.
- **Health checks** — Monitor whether your audit infrastructure is working; if your audit database fails, you'll know immediately instead of silently losing records.

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

### 1. Install and annotate

```bash
dotnet add package SensitiveFlow.AspNetCore.EFCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer # or your EF Core provider
```

Then mark your sensitive fields:

```csharp
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public class Customer
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;
}
```

### 2. Register and start

```csharp
using SensitiveFlow.AspNetCore.EFCore.Extensions;

// Register SensitiveFlow
builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);
    options.UseEfCoreStores(
        audit => audit.UseSqlServer("Server=.;Database=Audit;..."),
        tokens => tokens.UseSqlServer("Server=.;Database=Tokens;..."));
    
    options.EnableEfCoreAudit();
    options.EnableAspNetCoreContext();
    options.EnableJsonRedaction();
    options.EnableLoggingRedaction();
    options.EnableValidation();
    options.EnableHealthChecks();
});

// Wire the interceptor and middleware
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
    opt.UseSqlServer(connectionString)
        .AddInterceptors(sp.GetRequiredService<SensitiveDataAuditInterceptor>()));

var app = builder.Build();
app.UseSensitiveFlow();
app.MapHealthChecks("/health");
```

### 3. See the magic

Every time your code saves a customer, SensitiveFlow automatically:

- **Creates an audit record** showing who changed what field, when, and from what to what
- **Scrubs logs** — if a developer logs `email: {customer.Email}`, the actual email never appears in logs
- **Masks JSON responses** — API endpoints return `"email": "[REDACTED]"` instead of the real address
- **Tracks IP addresses** without storing them raw — instead stores a pseudonymized token
- **Validates retention policies** — knows when data should expire or be anonymized

No additional code needed. SensitiveFlow works at the infrastructure level.

### 4. Verify it works

Check the `/health` endpoint to ensure your audit and token infrastructure are connected:

```bash
curl http://localhost:5000/health
```

Success means your audit database is reachable and healthy.

### Schema setup

SensitiveFlow does not create tables automatically. Before your first write, create:

- Your application tables (via EF Core migrations)
- SensitiveFlow audit/token tables (via [SQL scripts](tools/migrations/) or EF Core)

See [Database Providers](docs/database-providers.md) for per-provider setup and schema details. The samples create schema automatically on startup for convenience; production apps should use migrations or deployment scripts.

## What You Get

| Feature | Example | Why |
|---------|---------|-----|
| **Audit trail** | `AuditRecord: customer.Email changed from john@old.com → john@new.com by UserId=abc at 2025-06-15T10:30:00Z` | Know who changed sensitive data and when; required for GDPR/LGPD investigation |
| **Log redaction** | `logger.LogInformation("User: {Name}, Email: {Email}", name, email)` → logs show `Email: [REDACTED]` | Prevent sensitive data in logs, error messages, or monitoring streams |
| **JSON masking** | `GET /api/customer/123` → `{"name":"John","email":"[REDACTED]","salary":"[REDACTED]"}` | API responses never leak sensitive fields even if you forget to scrub them |
| **IP pseudonymization** | IP `203.0.113.42` → token `ip_token_xyz` in logs; recoverable via token store | Audit shows request origin without storing raw IPs in plaintext |
| **Retention policies** | `[RetentionData(Years=5, AnonymizeOnExpiration)]` → field anonymized after 5 years automatically | Comply with "don't keep data longer than needed" requirement |
| **Data export** | Single API call to list all data associated with a user | GDPR/LGPD "right to access" — user can request and download their data |
| **Data erasure** | Schedule erasure or call API to fully delete user's data | GDPR/LGPD "right to be forgotten" — clean removal from all tables |

## Going Deeper

Once the basics work, explore:

- **[Durable Outbox](docs/audit.md#outbox)** — guaranteed audit delivery even if database is temporarily unavailable
- **[Retry & Backoff](docs/audit.md#retry)** — automatic exponential backoff with jitter for transient failures
- **[Profiles](docs/policies.md)** — switch from `Balanced` to `Strict` (redact everything) or `Permissive` (trust some fields)
- **[Custom policies](docs/policies.md)** — define your own classification rules
- **[OpenTelemetry](docs/diagnostics.md)** — emit audit events as spans and metrics for observability
- **[Caching token store](docs/anonymization.md#caching)** — speed up pseudonymization lookups with in-memory cache
- **[Data subject reports](docs/anonymization.md#export)** — customize what fields appear in privacy reports

See the [full documentation](docs/README.md) for all features.

## Samples

The repository includes runnable examples. After cloning:

```bash
cd samples/QuickStart.Sample
dotnet run

cd ../WebApi.Sample
dotnet run
```

Each sample demonstrates different configurations: QuickStart keeps it minimal, WebApi shows outbox and diagnostics, MinimalApi shows routing integration, Console shows standalone usage, Redis shows distributed token store.

## Advanced: Package-by-package setup

The composition layer (`SensitiveFlow.AspNetCore.EFCore`) is recommended. For teams needing fine-grained control over which packages to install:

See [Package Reference](docs/package-reference.md) for the full setup matrix and manual registration calls for each service.

> **Do not use in-memory stores in production.** Audit records and token mappings must survive process restarts.

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
