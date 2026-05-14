# SensitiveFlow.Analyzers.CodeFixes

Roslyn code fix providers for automatic remediation of analyzer violations.

## Provided Fixes

### SF0001 Fix — Add Masking Method
When analyzer detects unsanitized logging:

```csharp
// Before (violation)
logger.LogInformation("User email: {Email}", customer.Email);

// After (suggested fix)
logger.LogInformation("User email: {Email}", customer.Email.MaskEmail());
```

### SF0002 Fix — Add Masking in Response
When analyzer detects unsanitized response:

```csharp
// Before (violation)
return Ok(customer.Email);

// After (suggested fix)
return Ok(customer.Email.MaskEmail());
```

### SF0003 Fix — Add DataSubjectId Property
When analyzer detects missing identifier:

```csharp
// Before (violation)
public class Customer
{
    [PersonalData]
    public string Email { get; set; }
}

// After (suggested fix)
public class Customer
{
    public string DataSubjectId { get; set; }  // Added
    
    [PersonalData]
    public string Email { get; set; }
}
```

## Installation

```bash
dotnet add package SensitiveFlow.Analyzers.CodeFixes
```

## Integration

Works with:
- Visual Studio (Ctrl+.)
- VS Code (Cmd+. or Ctrl+.)
- JetBrains Rider

## Customization

Code fixes suggest standard methods:
- `MaskEmail()`
- `Redact()`
- `MaskPhone()`
- `RedactValue()`

For custom methods, manually apply then suppress the rule:

```csharp
#pragma warning disable SF0001
logger.LogInformation("Email: {Email}", MyCustomMask(customer.Email));
#pragma warning restore SF0001
```

## Possible Improvements

1. **Semantic fixes** — Understand context to suggest better masks
2. **Batch fix-all** — Fix all violations in file/solution
3. **Configuration-aware** — Suggest methods matching configured patterns
