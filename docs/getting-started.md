# Getting Started with SensitiveFlow

SensitiveFlow is a .NET library that brings observability and control to sensitive data at runtime. It automatically audits data access and mutations, redacts PII from logs, and provides masking and pseudonymization utilities — without requiring manual instrumentation.

## Installation

Install the packages you need:

```bash
dotnet add package SensitiveFlow.Core
dotnet add package SensitiveFlow.Audit
dotnet add package SensitiveFlow.EFCore
dotnet add package SensitiveFlow.AspNetCore
dotnet add package SensitiveFlow.Logging
dotnet add package SensitiveFlow.Anonymization
dotnet add package SensitiveFlow.Retention
```

## Step 1 — Annotate your model

Use `[PersonalData]` and `[SensitiveData]` to classify fields. These attributes drive automatic auditing and masking.

```csharp
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public class Customer
{
    public Guid Id { get; set; }

    // Required for audit record correlation
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;
}
```

## Step 2 — Register services

```csharp
// Program.cs

// Register your durable IAuditStore and ITokenStore implementations.
// Audit records must survive process restarts — there is no built-in in-memory store for production.
builder.Services.AddEfCoreAuditStore<MyDbContext>();  // your IAuditStore backed by SQL via EF Core
builder.Services.AddTokenStore<EfCoreTokenStore>();   // your ITokenStore backed by SQL, Redis, etc.

builder.Services.AddSensitiveFlowEFCore();       // registers SensitiveDataAuditInterceptor
builder.Services.AddSensitiveFlowAspNetCore();   // registers HttpAuditContext
builder.Services.AddSensitiveFlowLogging();      // registers DefaultSensitiveValueRedactor
builder.Services.AddRetention();                 // registers RetentionEvaluator
```

## Step 3 — Add the middleware

Place `UseSensitiveFlowAudit` before `UseAuthentication` so the pseudonymized IP token is available for all downstream middleware:

```csharp
app.UseSensitiveFlowAudit();
app.UseAuthentication();
app.UseAuthorization();
```

## Step 4 — Wire the EF Core interceptor

Register the interceptor when configuring your `DbContext`:

```csharp
builder.Services.AddDbContext<AppDbContext>((provider, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(provider.GetRequiredService<SensitiveDataAuditInterceptor>());
});
```

## Step 5 — Replace the audit context (optional)

By default the interceptor uses `NullAuditContext` (no actor, no IP). To enrich audit records with the HTTP request context, replace it:

```csharp
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowAspNetCore(); // registers HttpAuditContext as IAuditContext
```

## What happens at runtime

Every call to `SaveChanges` or `SaveChangesAsync` on your `DbContext` triggers the interceptor. For each entity in an `Added`, `Modified`, or `Deleted` state, it scans the properties for `[PersonalData]` or `[SensitiveData]` attributes and emits an `AuditRecord` per sensitive field:

```
AuditRecord {
  Id            = "...",
  DataSubjectId = "customer-42",
  Entity        = "Customer",
  Field         = "Email",
  Operation     = Update,
  Timestamp     = 2026-05-05T12:00:00Z,
  ActorId       = "operator-1",
  IpAddressToken = "pseudonymized-token"
}
```

## Next Steps

- [Attributes](attributes.md) — full reference for all attributes
- [Audit](audit.md) — querying and replacing the audit store
- [EF Core](efcore.md) — interceptor details
- [ASP.NET Core](aspnetcore.md) — middleware and HTTP context
- [Logging](logging.md) — PII redaction in structured logs
- [Retention](retention.md) — retention periods and expiration hooks
- [Anonymization](anonymization.md) — masking and pseudonymization utilities
