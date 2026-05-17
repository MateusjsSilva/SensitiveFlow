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

## Advanced Features

### Semantic-Aware Fix Suggestions
The code fix provider analyzes semantic context to suggest better masking methods:

```csharp
// Automatic detection by type and property name
var userEmail = customer.Email;        // Suggests MaskEmail()
var phoneNumber = customer.Phone;      // Suggests MaskPhone()
var creditCard = customer.CardNumber;  // Suggests MaskCreditCard()

// Falls back to heuristic matching if semantic info unavailable
var customField = data.SensitiveValue; // Suggests Redact() as safe default
```

**Components:**
- `SemanticAnalysisHelper` — Type-based and heuristic mask method selection
- `ExpressionContextType` — Identifies expression context (logging, response, database)
- Uses `SemanticModel` for accurate type information when available

### Batch Fix-All in File/Solution
Fix all violations in a file or entire solution with a single action:

```csharp
// Before (in Visual Studio)
// Right-click violation → "Fix all in file" or "Fix all in solution"
// All SF0001/SF0002 violations fixed automatically

// Code fix applies to ALL matching diagnostics, ordered by position
// to avoid offset issues during simultaneous application
```

**Components:**
- `BatchFixProvider` — Implements `FixAllProvider` for scope-aware batch fixing
- Handles document-grouped diagnostics with proper offset handling
- Supports file, project, and solution-wide fix-all

### Configuration-Aware Masking Methods
Configure custom masking method patterns per property name:

```csharp
var config = new CodeFixConfiguration();
config.AddCustomPattern("email", "MaskEmail");
config.AddCustomPattern("ssn", "MaskSsn");
config.AddCustomPattern("card*", "MaskCreditCard");  // Wildcard matching

// Code fixes now suggest registered methods instead of defaults
var method = config.GetMaskingMethodForProperty("customerEmail");  // Returns "MaskEmail"
var method = config.GetMaskingMethodForProperty("cardNumber");     // Returns "MaskCreditCard"
```

**Components:**
- `CodeFixConfiguration` — Centralized configuration registry
- `RecognizedMaskingMethods` — Whitelist of allowed masking methods
- `CustomMaskingPatterns` — Property name → method mapping with wildcard support
- `AddCustomPattern()` — Dynamic pattern registration
