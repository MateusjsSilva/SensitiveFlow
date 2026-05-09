# Getting Started with SensitiveFlow

SensitiveFlow is a .NET library that brings observability and control to sensitive data at runtime. It audits EF Core changes, redacts sensitive JSON/log output, and provides masking, pseudonymization, retention, export, and erasure utilities.

## Installation

Install only the packages used by the app:

```bash
dotnet add package SensitiveFlow.Core
dotnet add package SensitiveFlow.Audit
dotnet add package SensitiveFlow.Audit.EFCore
dotnet add package SensitiveFlow.EFCore
dotnet add package SensitiveFlow.AspNetCore
dotnet add package SensitiveFlow.Anonymization
dotnet add package SensitiveFlow.Json
dotnet add package SensitiveFlow.Logging
dotnet add package SensitiveFlow.Retention
dotnet add package SensitiveFlow.Diagnostics
```

## Step 1 - Annotate your model

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

## Step 2 - Register the recommended web stack

This is the full ASP.NET Core + EF Core setup. For a smaller app, remove the packages you do not need.

```csharp
builder.Services.AddEfCoreAuditStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Audit")));
builder.Services.AddAuditStoreRetry();
builder.Services.AddSensitiveFlowDiagnostics();

builder.Services.AddEfCoreTokenStore(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Tokens")));
builder.Services.AddCachingTokenStore();

builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowAspNetCore();
builder.Services.AddSensitiveFlowLogging();
builder.Services.AddDataSubjectExport();
builder.Services.AddDataSubjectErasure();
builder.Services.AddRetention();
builder.Services.AddRetentionExecutor();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.WithSensitiveDataRedaction());
```

The token store is the one piece you still provide today. It must be durable because losing token mappings makes reversible pseudonymization impossible.

## Step 3 - Wire your DbContext

```csharp
builder.Services.AddDbContext<AppDbContext>((provider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("App"));
    options.AddInterceptors(provider.GetRequiredService<SensitiveDataAuditInterceptor>());
});
```

## Step 4 - Add the middleware

```csharp
app.UseSensitiveFlowAudit();
app.UseAuthentication();
app.UseAuthorization();
```

Configure forwarded headers before `UseSensitiveFlowAudit` when the app runs behind a proxy/load balancer.

## Runtime behavior

Every `SaveChangesAsync` call scans changed entities for `[PersonalData]` or `[SensitiveData]` and writes audit records through `IAuditStore`. HTTP middleware fills `ActorId`/`IpAddressToken`, JSON redaction protects serialized output, and retention/export/erasure services are called explicitly by your jobs or endpoints.

## Next Steps

- [Package reference](package-reference.md): package-by-package setup matrix.
- [Audit](audit.md): retry, buffering, query, retention, and snapshot concepts.
- [EF Core](efcore.md): interceptor behavior and entity requirements.
- [ASP.NET Core](aspnetcore.md): request context and IP pseudonymization.
- [JSON redaction](json.md): `System.Text.Json` output protection.
- [Anonymization](anonymization.md): token stores, masking, export, erasure, fingerprints.
- [Retention](retention.md): scheduled retention evaluation and execution.
