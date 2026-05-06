# SensitiveFlow

[![CI](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/ci.yml)
[![CodeQL](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/codeql.yml/badge.svg)](https://github.com/MateusjsSilva/SensitiveFlow/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/SensitiveFlow.Core)](https://www.nuget.org/packages/SensitiveFlow.Core)

**SensitiveFlow** is a .NET library that brings observability and control to sensitive data. It focuses on runtime behavior -- automatic auditing, log redaction, and masking -- not compliance paperwork.

## Why SensitiveFlow?

Sensitive data flows through your application on every request: EF Core saves, HTTP responses, log lines. SensitiveFlow makes that flow visible and controlled at the infrastructure level, without requiring manual instrumentation.

## Packages

| Package | Description | Status |
|---------|-------------|--------|
| `SensitiveFlow.Core` | Attributes, enums, interfaces, models, exceptions | Preview |
| `SensitiveFlow.Audit` | Immutable audit trail -- bring your own durable store | Preview |
| `SensitiveFlow.EFCore` | SaveChanges interceptor for automatic auditing | Preview |
| `SensitiveFlow.AspNetCore` | Middleware for actor/IP context enrichment | Preview |
| `SensitiveFlow.Logging` | ILogger decorator for PII redaction in logs | Preview |
| `SensitiveFlow.Anonymization` | Masking, anonymization, and pseudonymization | Preview |
| `SensitiveFlow.Retention` | Retention metadata and expiration hook contracts | Preview |
| `SensitiveFlow.Analyzers` | Roslyn analyzers for privacy anti-patterns | Preview |

## Quick Start

### 1. Install packages

```bash
dotnet add package SensitiveFlow.Core
dotnet add package SensitiveFlow.Audit
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

### 3. Implement and register a durable audit store

`IAuditStore` is the persistence contract -- you own the implementation so audit records go
exactly where your infrastructure requires (SQL, MongoDB, Azure Table Storage, etc.).

```csharp
// Your implementation backed by a real database:
public sealed class EfCoreAuditStore : IAuditStore
{
    private readonly AuditDbContext _db;

    public EfCoreAuditStore(AuditDbContext db) => _db = db;

    public async Task AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        _db.AuditRecords.Add(record);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var query = _db.AuditRecords.AsQueryable();
        if (from.HasValue) query = query.Where(r => r.Timestamp >= from.Value);
        if (to.HasValue)   query = query.Where(r => r.Timestamp <= to.Value);
        return await query.OrderBy(r => r.Timestamp).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var query = _db.AuditRecords.Where(r => r.DataSubjectId == dataSubjectId);
        if (from.HasValue) query = query.Where(r => r.Timestamp >= from.Value);
        if (to.HasValue)   query = query.Where(r => r.Timestamp <= to.Value);
        return await query.OrderBy(r => r.Timestamp).Skip(skip).Take(take).ToListAsync(ct);
    }
}
```

```csharp
// Program.cs
builder.Services.AddAuditStore<EfCoreAuditStore>();
```

> **Do not use an in-memory store in production.** Audit records must survive process
> restarts. Losing audit history violates the accountability principle under LGPD Art. 37
> and GDPR Art. 5(2).

### 4. Register services

```csharp
builder.Services.AddAuditStore<EfCoreAuditStore>();  // your durable store
builder.Services.AddSensitiveFlowEFCore();           // SensitiveFlow.EFCore
builder.Services.AddSensitiveFlowAspNetCore();       // SensitiveFlow.AspNetCore
builder.Services.AddSensitiveFlowLogging();          // SensitiveFlow.Logging
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

Every `SaveChanges` on a field annotated with `[PersonalData]` or `[SensitiveData]` now
produces an `AuditRecord` automatically.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Attributes](docs/attributes.md)
- [Audit](docs/audit.md)
- [EF Core](docs/efcore.md)
- [ASP.NET Core](docs/aspnetcore.md)
- [Logging](docs/logging.md)
- [Retention](docs/retention.md)
- [Anonymization](docs/anonymization.md)
- [Analyzers](docs/analyzers.md)

## Design Principles

- **Runtime behavior over compliance paperwork** -- instruments what actually happens, not what should happen.
- **Explicit metadata over implicit heuristics** -- every classification is opt-in via attributes.
- **Composition over lock-in** -- each module is optional and independently testable.
- **Safe defaults** -- the IP address is never stored raw; the log redactor strips sensitive values before they reach any sink.
- **Bring your own persistence** -- `IAuditStore` and `ITokenStore` are contracts, not implementations. You choose the database.

## Status

Work in progress. See [PLAN.md](PLAN.md) for the full roadmap.

## License

MIT
