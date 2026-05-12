# Analyzers

`SensitiveFlow.Analyzers` provides Roslyn diagnostics that catch common privacy anti-patterns
at compile time, before they reach production.

## Installation

Add the package to any project that contains models or handlers you want to guard:

```bash
dotnet add package SensitiveFlow.Analyzers
```

In application projects, keep analyzers private to that project:

```xml
<PackageReference Include="SensitiveFlow.Analyzers" Version="1.0.0-preview.3" PrivateAssets="all" />
```

During local development of this repository, reference the analyzer project as an
analyzer instead of as a normal library:

```xml
<ProjectReference Include="..\src\SensitiveFlow.Analyzers\SensitiveFlow.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Install `SensitiveFlow.Analyzers.CodeFixes` as well if you want IDE quick fixes for
SF0001/SF0002.

The diagnostics run automatically during `dotnet build` and in real-time inside VS Code,
Visual Studio, and Rider via their Roslyn analysis pipelines.

## Rules

### SF0001 -- Sensitive data logged directly

**Severity:** Warning

Triggers when a property or field annotated with `[PersonalData]` or `[SensitiveData]`
is passed as an argument to any `ILogger` / `LoggerExtensions` method without first going
through a masking or redaction transform.

**Detected pattern:**

```csharp
[PersonalData]
public string Email { get; set; }

// SF0001: Sensitive member 'Email' is being logged without masking or redaction
logger.LogInformation("user email: {Email}", customer.Email);
```

**Compliant pattern:**

```csharp
// Pass through a masking method -- any name containing Mask, Redact, Anonymize,
// Pseudonymize, or Hash suppresses the diagnostic.
logger.LogInformation("user email: {Email}", customer.Email.MaskEmail());
logger.LogInformation("user email: {Email}", Redact(customer.Email));
```

**Recognized logging targets:** `ILogger` and `LoggerExtensions` (Microsoft.Extensions.Logging).

---

### SF0002 -- Sensitive data returned directly in HTTP responses

**Severity:** Warning

Triggers when a property or field annotated with `[PersonalData]` or `[SensitiveData]`
is returned directly via an HTTP response factory or from an HTTP endpoint method without
a masking or redaction transform.

**Detected patterns:**

```csharp
[PersonalData]
public string Email { get; set; }

// SF0002: via controller response factory
[HttpGet("{id}")]
public IActionResult Get(Customer customer)
{
    // SF0002: Sensitive member 'Email' is being returned in an HTTP response
    return Ok(customer.Email);
}

// SF0002: via minimal API Results
app.MapGet("/customers/{id}", (Customer customer) =>
    Results.Ok(customer.Email)); // SF0002
```

**Compliant pattern:**

```csharp
return Ok(customer.Email.MaskEmail());
return Results.Ok(new CustomerResponse(customer.Email.MaskEmail()));
```

**Recognized response factories:** `ControllerBase.Ok`, `Results.Ok`, `Results.Json`,
`Results.Created`, `Results.CreatedAtRoute`, `TypedResults.*`.

---

### SF0003 -- Entity missing DataSubjectId

**Severity:** Warning

Triggers when a class declares members annotated with `[PersonalData]` or `[SensitiveData]`
but does not expose a public `DataSubjectId` (or legacy `UserId`) property. The EF Core
interceptor requires a stable subject identifier and throws at runtime if it cannot resolve one.

**Detected pattern:**

```csharp
public sealed class Customer
{
  [PersonalData]
  public string Email { get; set; } = string.Empty;
}
```

**Compliant pattern:**

```csharp
public sealed class Customer
{
  public string DataSubjectId { get; set; } = string.Empty;

  [PersonalData]
  public string Email { get; set; } = string.Empty;
}
```

---

### SF0004 -- Property name suggests unannotated personal data

**Severity:** Info

Triggers when a public property name looks like personal data, such as `Email`, `Phone`,
`TaxId`, `Cpf`, `Ssn`, `Passport`, `Address`, `BirthDate`, or `IpAddress`, but the
property is not annotated with `[PersonalData]` or `[SensitiveData]`.

**Detected pattern:**

```csharp
public sealed class Customer
{
    public string Email { get; set; } = string.Empty; // SF0004
}
```

**Compliant pattern:**

```csharp
public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}
```

---

## How Suppression Works

The analyzer considers a value safe when it passes through a method whose name contains
any of: `Mask`, `Redact`, `Anonymize`, `Pseudonymize`, `Hash` (case-insensitive).
This covers the built-in transforms in `SensitiveFlow.Anonymization` as well as any
custom helpers you write.

## Tuning Severity

Use `.editorconfig` to adjust per-rule severity across the entire solution or per folder:

```ini
[*.cs]
dotnet_diagnostic.SF0001.severity = warning
dotnet_diagnostic.SF0002.severity = warning
dotnet_diagnostic.SF0003.severity = warning
dotnet_diagnostic.SF0004.severity = suggestion
```

Available severities: `error`, `warning`, `suggestion`, `silent`, `none`.

Prefer `error` in new code to prevent regressions from being merged:

```ini
[src/**/*.cs]
dotnet_diagnostic.SF0001.severity = error
dotnet_diagnostic.SF0002.severity = error
dotnet_diagnostic.SF0003.severity = error
dotnet_diagnostic.SF0004.severity = warning
```

## Known Limitations

- **`ILogger<T>` not yet detected.** SF0001 currently matches `ILogger` (non-generic)
  and `LoggerExtensions`. If you inject `ILogger<T>`, the diagnostic will not fire.
  Workaround: the `LoggerExtensions` static methods (e.g. `.LogInformation(...)`) are
  still detected regardless of the generic type parameter.

- **Minimal API lambdas.** SF0002 does not detect sensitive members returned directly
  from `MapGet`/`MapPost` delegates that are not annotated with HTTP route attributes.
  Use `Results.Ok(...)` (which is detected) rather than bare `return value;`.

- **Whole-object serialization.** Passing a full entity instance (`Ok(customer)`) where
  the entity contains `[PersonalData]` properties is not yet detected. Use response DTOs
  that contain only masked values.
