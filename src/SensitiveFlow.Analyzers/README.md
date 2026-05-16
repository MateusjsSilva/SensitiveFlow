# SensitiveFlow.Analyzers

Roslyn diagnostics that catch common privacy anti-patterns at compile-time before they reach production.

## Diagnostics Rules

### SF0001 — Sensitive data logged directly
**Severity**: Warning

Triggers when a property annotated with `[PersonalData]` or `[SensitiveData]` is passed to any `ILogger` or `LoggerExtensions` method without masking/redaction.

**Detected pattern**:
```csharp
logger.LogInformation("user email: {Email}", customer.Email); // SF0001
```

**Compliant patterns**:
```csharp
logger.LogInformation("user email: {Email}", customer.Email.MaskEmail());
logger.LogInformation("user email: {Email}", Redact(customer.Email));
```

**Recognized targets**: `ILogger`, `LoggerExtensions`

### SF0002 — Sensitive data returned directly in HTTP responses
**Severity**: Warning

Triggers when a property annotated with `[PersonalData]` or `[SensitiveData]` is returned via HTTP response factory or endpoint method without masking/redaction.

**Detected patterns**:
```csharp
return Ok(customer.Email); // SF0002
return Results.Ok(customer.Email); // SF0002
```

**Compliant patterns**:
```csharp
return Ok(customer.Email.MaskEmail());
return Results.Ok(new CustomerResponse(customer.Email.MaskEmail()));
```

**Recognized targets**: `ControllerBase.Ok`, `Results.Ok`, `Results.Json`, `Results.Created`, `Results.CreatedAtRoute`, `TypedResults.*`

### SF0003 — Entity missing DataSubjectId
**Severity**: Error

Triggers when a class declares members with `[PersonalData]` or `[SensitiveData]` but lacks a public `DataSubjectId` (or `UserId` alias). EF Core interceptor requires this at compile-time. Build will fail without it.

**Detected pattern**:
```csharp
public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; }
}
```

**Compliant pattern**:
```csharp
public sealed class Customer
{
    public string DataSubjectId { get; set; }
    [PersonalData]
    public string Email { get; set; }
}
```

### SF0004 — Property name suggests unannotated personal data
**Severity**: Info

Triggers when a public property name looks like personal data (Email, Phone, TaxId, SSN, Passport, Address, BirthDate, IpAddress) but is not annotated.

**Detected pattern**:
```csharp
public string Email { get; set; } // SF0004
```

**Compliant pattern**:
```csharp
[PersonalData]
public string Email { get; set; }
```

### SF0005 — Sensitive data returned from endpoint without authorization
**Severity**: Warning

Triggers when a method on `[ApiController]` returns a type containing `[PersonalData]` or `[SensitiveData]` properties but lacks `[Authorize]` attribute.

**Detected pattern**:
```csharp
[ApiController]
public sealed class CustomersController
{
    [HttpGet("{id}")]
    public CustomerResponse GetCustomer(string id) // SF0005: no [Authorize]
    {
        return new CustomerResponse { Email = "alice@example.com" };
    }
}
```

**Compliant patterns**:
```csharp
[Authorize]
[HttpGet("{id}")]
public CustomerResponse GetCustomer(string id) { ... }

// OR return a DTO without sensitive fields
public CustomerPublicResponse GetCustomer(string id) { ... }
```

### SF0006 — Sensitive data property missing [Redaction] attribute
**Severity**: Error

Triggers when a property annotated with `[PersonalData]` or `[SensitiveData]` lacks an explicit `[Redaction]` attribute. Without redaction configuration, the property exposes full PII in API responses, logs, and audit trails.

**Detected pattern**:
```csharp
public class Customer
{
    [PersonalData]
    public string Email { get; set; }  // SF0006: No [Redaction]
    
    [SensitiveData]
    public string ApiKey { get; set; }  // SF0006: No [Redaction]
}
```

**Compliant pattern**:
```csharp
public class Customer
{
    [PersonalData]
    [Redaction(
        ApiResponse = OutputRedactionAction.Mask,
        Logs = OutputRedactionAction.Redact,
        Audit = OutputRedactionAction.Mask,
        Export = OutputRedactionAction.None
    )]
    public string Email { get; set; }
    
    [SensitiveData]
    [Redaction(
        ApiResponse = OutputRedactionAction.Redact,
        Logs = OutputRedactionAction.Redact,
        Audit = OutputRedactionAction.Redact
    )]
    public string ApiKey { get; set; }
}
```

Alternatively, use shorthand attributes:
```csharp
[PersonalData]
[Mask(MaskKind.Email)]
public string Email { get; set; }

[SensitiveData]
[Redact]
public string ApiKey { get; set; }
```

**Why Error Severity**: Marking data sensitive but not specifying redaction likely indicates an incomplete implementation. It defeats the purpose of annotation — data is flagged as sensitive but has no protection configured.

## Suppression

The analyzer considers a value safe when it passes through a method whose name contains: `Mask`, `Redact`, `Anonymize`, `Pseudonymize`, `Hash` (case-insensitive).

## Severity Configuration

Use `.editorconfig` to adjust severity:

```ini
[*.cs]
dotnet_diagnostic.SF0001.severity = warning
dotnet_diagnostic.SF0002.severity = error
dotnet_diagnostic.SF0003.severity = error
dotnet_diagnostic.SF0004.severity = suggestion
dotnet_diagnostic.SF0005.severity = warning
dotnet_diagnostic.SF0006.severity = error
```

Note: SF0003 is now **Error-level** (enforced compile-time), not Warning. Build will fail without DataSubjectId on sensitive entities.

## Installation

```bash
dotnet add package SensitiveFlow.Analyzers

# Optional: code fixes for SF0001/SF0002
dotnet add package SensitiveFlow.Analyzers.CodeFixes
```

In application projects, keep private:

```xml
<PackageReference Include="SensitiveFlow.Analyzers" Version="x.y.z" PrivateAssets="all" />
```

## Known Limitations

1. **`ILogger<T>` not detected** — Only `ILogger` and static `LoggerExtensions` detected. Workaround: use static methods.
2. **Minimal API lambdas** — Direct returns from `MapGet`/`MapPost` not detected. Use `Results.Ok(...)`.
3. **Whole-object serialization** — Passing full entity instances (`Ok(customer)`) not detected. Use response DTOs.

## Possible Improvements

1. **Generic ILogger<T> support** — Complex due to generic type parameter tracking
2. **Attribute-based exclusion** — Allow `[DoNotAnalyze]` to suppress per-property
3. **Custom masking method detection** — Allow configurable method name patterns
4. **Cross-assembly analysis** — Analyze caller chains across projects
