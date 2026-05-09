# AI skill: using SensitiveFlow correctly

Use this document as instructions for an AI coding agent that needs to add or modify SensitiveFlow usage in a .NET application.

## Goal

Help the application control personal data flows at runtime: audit writes, redact serialized output/logs, pseudonymize reversible identifiers, and evaluate retention policies.

## Required reading order

1. `docs/package-reference.md`
2. `docs/getting-started.md`
3. Relevant package doc:
   - audit: `docs/audit.md`
   - EF Core: `docs/efcore.md`
   - ASP.NET Core: `docs/aspnetcore.md`
   - anonymization: `docs/anonymization.md`
   - JSON: `docs/json.md`
   - logging: `docs/logging.md`
   - retention: `docs/retention.md`
   - analyzers: `docs/analyzers.md`
   - testkit: `docs/testkit.md`

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

- `ITokenStore` must be durable.
- Add `TokenStoreContractTests` for custom stores.
- Add a unique constraint on original value and token in database-backed stores.
- Consider `AddCachingTokenStore(...)` for repeated hot values after accepting the in-memory exposure trade-off.

Use `HmacPseudonymizer` when deterministic non-reversible tokens are enough.

Rules:

- Secret must be strong and stable.
- Rotation invalidates previous tokens unless handled deliberately.

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
- Update docs when adding a new integration path.

## Avoid

- Do not use in-memory token/audit stores in production.
- Do not call sync `SaveChanges` in ASP.NET Core request paths.
- Do not describe masking as anonymization.
- Do not store raw IP addresses in audit records.
- Do not suppress analyzer findings without a documented reason.
- Do not add buffered audit as a default until the app accepts possible in-memory loss.

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

### Store contract test

```csharp
public sealed class SqlTokenStoreTests : TokenStoreContractTests
{
    protected override Task<ITokenStore> CreateStoreAsync()
        => Task.FromResult<ITokenStore>(new SqlTokenStore(...));
}
```

### Redaction assertion

```csharp
var payload = JsonSerializer.Serialize(customer, options);
SensitiveDataAssert.DoesNotLeak(payload, customer);
```

## Review checklist for AI agents

When reviewing a SensitiveFlow change, check:

- Are annotated values protected at every output boundary?
- Does audit use a durable store?
- Are audit writes batched or retried where needed?
- Is the token store durable and concurrency-safe?
- Are sync-over-async paths avoided in ASP.NET Core?
- Are provider-specific persistence paths tested?
- Are docs and examples using real public APIs?
