# Getting Started with SensitiveFlow

SensitiveFlow is a .NET library that brings observability and control to sensitive data at runtime. It audits EF Core changes, redacts sensitive JSON/log output, and provides masking, pseudonymization, retention, export, and erasure utilities.

## Quick Start (recommended)

Install the single composition package:

```bash
dotnet add package SensitiveFlow.AspNetCore.EFCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer # or your EF Core provider
```

This brings in the full recommended ASP.NET Core + EF Core stack. The database
provider package remains app-owned, so the same SensitiveFlow setup works with any
EF Core provider.

The runnable samples in this repository target .NET 10. Use the .NET 10 SDK when
running `samples/QuickStart.Sample`, `samples/MinimalApi.Sample`, or
`samples/WebApi.Sample`.

### Step 1 - Annotate your model

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

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;
}
```

`DataSubjectId` or `UserId` is required for EF Core audit correlation.

### Step 2 - Register SensitiveFlow

```csharp
using SensitiveFlow.AspNetCore.EFCore.Extensions;

builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);

    options.UseEfCoreStores(
        audit => audit.UseSqlServer(builder.Configuration.GetConnectionString("Audit")),
        tokens => tokens.UseSqlServer(builder.Configuration.GetConnectionString("Tokens")));

    options.EnableEfCoreAudit();
    options.EnableAspNetCoreContext();
    options.EnableLoggingRedaction();
    options.EnableJsonRedaction();
    options.EnableValidation();
    options.EnableHealthChecks();

    // Enable as needed:
    // options.EnableOutbox();
    // options.EnableDiagnostics();
    // options.EnableAuditStoreRetry();
    // options.EnableCachingTokenStore();
    // options.EnableRetention().EnableRetentionExecutor();
    // options.EnableDataSubjectExport().EnableDataSubjectErasure();
});
```

### Step 3 - Wire your DbContext and middleware

```csharp
builder.Services.AddDbContext<AppDbContext>((provider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("App"));
    options.AddInterceptors(provider.GetRequiredService<SensitiveDataAuditInterceptor>());
});

var app = builder.Build();

// Place before UseAuthentication.
app.UseSensitiveFlow();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
```

### What you get

| Feature | How it works |
|---------|-------------|
| EF Core audit | Every `SaveChangesAsync` on `[PersonalData]`/`[SensitiveData]` fields writes `AuditRecord` |
| HTTP context | Middleware fills `ActorId`/`IpAddressToken` (pseudonymized) |
| JSON redaction | `System.Text.Json` output is masked/redacted automatically |
| Log redaction | `[Sensitive]` markers and annotated members are scrubbed |
| Startup validation | Verifies configured infrastructure at startup |
| Health checks | `/health` endpoint monitors audit and token stores |

### Next steps

- [Package reference](package-reference.md): full package-by-package reference and the `SensitiveFlow.AspNetCore.EFCore` composition layer.
- [Audit](audit.md): retry, buffering, outbox, snapshots, and query concepts.
- [EF Core](efcore.md): interceptor behavior and entity requirements.
- [ASP.NET Core](aspnetcore.md): request context and IP pseudonymization.
- [JSON redaction](json.md): `System.Text.Json` output protection.
- [Anonymization](anonymization.md): token stores, masking, export, erasure, fingerprints.
- [Retention](retention.md): scheduled retention evaluation and execution.

## Advanced: per-package setup

If you need precise control over which packages are installed and how each service
is registered, see the [Package Reference](package-reference.md) for a full
package-by-package setup matrix with individual registration calls.

The composition package is the recommended path for new adopters. The granular
extension methods (e.g. `AddEfCoreAuditStore`, `AddSensitiveFlowEFCore`) continue
to work and are documented in the package reference for teams that need them.

## Database schema

SensitiveFlow does not create tables automatically. The app tables and the
SensitiveFlow audit/token/outbox tables must exist before the first write.

Three options, in order of preference:

1. **EF Core migrations** owned by your app — best for teams already using EF Core.
2. **Idempotent SQL scripts** shipped in [`tools/migrations/`](../tools/migrations/) — one folder per supported provider (`sqlite`, `sqlserver`, `postgres`). See [Database providers](database-providers.md) for the full matrix and provider-specific notes.
3. **`EnsureCreatedAsync()`** — fine for samples and tests, **not for production**: it skips migrations entirely.

If the schema is missing at runtime, SensitiveFlow now throws
`SensitiveFlowSchemaNotInitializedException` (code `SF-SCHEMA-001`) instead of
leaking a raw provider error. The message points you back to the migration scripts.

The audit outbox dispatcher is also defensive: if polling fails because the
durable outbox table is absent or the database is unavailable, it logs the
infrastructure failure and suspends polling by default instead of stopping the
application host. Apply the schema and restart the app.

The repository samples (`QuickStart`, `MinimalApi`, `WebApi`) call
`EnsureCreatedAsync()` on startup so you can try them immediately. **Do not copy
that pattern into production apps** — use migrations or the scripts in
`tools/migrations/`.
