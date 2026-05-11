# Policies, Discovery, Health, and Startup Validation

## Policy engine and profiles

Use `SensitiveFlowOptions` when an application wants one central place to declare how categories should be handled. Applications that reference `SensitiveFlow.Diagnostics` can register the shared options with `AddSensitiveFlow(...)`.

```csharp
builder.Services.AddSensitiveFlow(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);

    options.Policies.ForCategory(DataCategory.Contact)
        .MaskInLogs()
        .RedactInJson()
        .AuditOnChange();

    options.Policies.ForSensitiveCategory(SensitiveDataCategory.Health)
        .OmitInJson()
        .RequireAudit();
});
```

Built-in profiles:

- `Development`: useful local defaults with masking and lighter audit requirements.
- `Balanced`: API/log redaction plus audit for common categories.
- `Strict`: omission/redaction and required audit for sensitive categories.
- `AuditOnly`: audit-focused hints without forcing output behavior.

## Defaults

SensitiveFlow defaults are intentionally conservative and documented in `SensitiveFlowDefaults`:

| Setting | Default |
| --- | --- |
| Profile | `SensitiveFlowProfile.Balanced` |
| JSON redaction mode | `JsonRedactionMode.Mask` |
| Full redaction placeholder | `[REDACTED]` |
| Retention anonymization marker | `[ANONYMIZED]` |
| Audit health check name | `sensitiveflow-audit-store` |
| Token health check name | `sensitiveflow-token-store` |
| Audit outbox health check name | `sensitiveflow-audit-outbox` |
| Logging redactor | redacts marked values to `[REDACTED]` |
| CLI scan input | compiled assembly, project/solution file, or directory containing a single project/solution or `.dll` files |

Precedence for JSON output is:

1. `[JsonRedaction]`
2. `[Redaction(ApiResponse = ...)]`
3. `[Omit]`, `[Redact]`, `[Mask]`
4. category policies (`OmitInJson`, then `RedactInJson`)
5. `JsonRedactionOptions.DefaultMode`

Pass policies into JSON redaction explicitly:

```csharp
var sensitiveFlow = SensitiveFlowPolicyConfiguration.Create(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);
    options.Policies.ForCategory(DataCategory.Contact).RedactInJson();
});

builder.Services.AddSingleton(sensitiveFlow);
builder.Services.AddSensitiveFlowJsonRedaction(options =>
{
    options.DefaultMode = JsonRedactionMode.Mask;
    options.Policies = sensitiveFlow.Policies;
});
```

## Discovery report

`SensitiveDataDiscovery` scans compiled assemblies for `[PersonalData]`, `[SensitiveData]`, and `[RetentionData]`.

```csharp
var report = SensitiveDataDiscovery.Scan(typeof(Customer).Assembly);

File.WriteAllText("sensitiveflow-report.json", report.ToJson());
File.WriteAllText("sensitiveflow-report.md", report.ToMarkdown());
```

The report includes type, member, annotation kind, category, sensitivity, and retention settings.

## CLI tool

`SensitiveFlow.Tool` exposes the same report generator for CI and documentation jobs. It accepts a compiled assembly, a project/solution file, or a directory. When the input is a `.csproj`, `.sln`, `.slnx`, or a directory containing a single project/solution, the tool runs `dotnet build -c Release` first and then scans the compiled assemblies.

```bash
dotnet tool install SensitiveFlow.Tool
sensitiveflow scan ./src/MyApp/MyApp.csproj ./artifacts/privacy
sensitiveflow scan ./bin/Release/net10.0/MyApp.dll ./artifacts/privacy
sensitiveflow scan ./src/MyApp/bin/Release/net10.0 ./artifacts/privacy
```

The tool writes:

- `sensitiveflow-report.json`
- `sensitiveflow-report.md`

The CLI also scans source files for production footguns. `SF-CLI-001` is emitted when `AddInMemoryAuditOutbox()` appears outside a `#if DEBUG` branch. The warning does not fail the scan, but it should be treated as release-blocking for production apps.

## Health checks

`SensitiveFlow.HealthChecks` integrates with `Microsoft.Extensions.Diagnostics.HealthChecks`.

```csharp
builder.Services.AddSensitiveFlowHealthChecks()
    .AddAuditStoreCheck()
    .AddTokenStoreCheck()
    .AddAuditOutboxCheck();
```

`IAuditStore` is checked with a read-only `QueryAsync(take: 1)` probe. `ITokenStore` is resolution-only by default because the contract has no read-only ping; stores can implement `IHealthProbe` for a real non-mutating probe. `IAuditOutbox` is healthy when a durable or custom outbox is registered, and reports `Degraded` when `InMemoryAuditOutbox` is used outside Development.

## Startup validation

`SensitiveFlow.Diagnostics` includes a startup self-test for common misconfiguration. It checks required stores, policy-driven JSON/logging/audit requirements, EF Core audit interceptor registration, loaded retention annotations, and ASP.NET Core middleware registration markers when those packages are present.

```csharp
builder.Services.AddSensitiveFlowValidation(options =>
{
    options.RequireAuditStore = true;
    options.RequireTokenStore = true;
});

var result = app.Services.ValidateSensitiveFlow();
```

Example warnings:

- `SF-CONFIG-001`: no `IAuditStore` registration found.
- `SF-CONFIG-002`: no durable `ITokenStore` registration found.
- `SF-CONFIG-003`: `IPseudonymizer` registered without `ITokenStore`.
- `SF-CONFIG-009`: EF Core audit interceptor registered without `IAuditStore`.
- `SF-CONFIG-010`: retention annotations found without `RetentionExecutor` or handlers.
- `SF-CONFIG-011`: ASP.NET Core services registered but `UseSensitiveFlowAudit()` was not marked.
- `SF-CONFIG-012`: middleware observed an authenticated user before it ran, which can indicate it was placed after authentication.
- `SF-CONFIG-013`: in-memory audit outbox registered outside Development.
- `SF-CONFIG-014`: durable audit outbox registered without any `IAuditOutboxPublisher`.
