# Policies, Discovery, Health, and Startup Validation

## Policy engine and profiles

Use `SensitiveFlowOptions` when an application wants one central place to declare how categories should be handled.

```csharp
var options = SensitiveFlowPolicyConfiguration.Create(options =>
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

## Discovery report

`SensitiveDataDiscovery` scans compiled assemblies for `[PersonalData]`, `[SensitiveData]`, and `[RetentionData]`.

```csharp
var report = SensitiveDataDiscovery.Scan(typeof(Customer).Assembly);

File.WriteAllText("sensitiveflow-report.json", report.ToJson());
File.WriteAllText("sensitiveflow-report.md", report.ToMarkdown());
```

The report includes type, member, annotation kind, category, sensitivity, and retention settings.

## CLI tool

`SensitiveFlow.Tool` exposes the same report generator for CI and documentation jobs.

```bash
dotnet tool install SensitiveFlow.Tool
sensitiveflow scan ./bin/Release/net10.0/MyApp.dll ./artifacts/privacy
```

The tool writes:

- `sensitiveflow-report.json`
- `sensitiveflow-report.md`

## Health checks

`SensitiveFlow.HealthChecks` integrates with `Microsoft.Extensions.Diagnostics.HealthChecks`.

```csharp
builder.Services.AddSensitiveFlowHealthChecks()
    .AddAuditStoreCheck()
    .AddTokenStoreCheck();
```

`IAuditStore` is checked with a read-only `QueryAsync(take: 1)` probe. `ITokenStore` is resolution-only by default because the contract has no read-only ping; stores can implement `IHealthProbe` for a real non-mutating probe.

## Startup validation

`SensitiveFlow.Diagnostics` includes a startup self-test for common misconfiguration.

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
