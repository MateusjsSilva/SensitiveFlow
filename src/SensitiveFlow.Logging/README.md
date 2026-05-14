# SensitiveFlow.Logging

Logging integration that redacts sensitive data automatically in log messages.

## Main Components

### RedactingLogger
- **`RedactingLogger<T>`** — `ILogger<T>` wrapper that intercepts log calls
  - Scans message template and arguments for sensitive data
  - Applies `[Redaction(Logs=...)]` per field
  - Supports all log levels and message formats
  - Lazy evaluation (no processing if log level disabled)

### Integration
- **`AddSensitiveFlowLogging()`** — Registers logger factory decorator
- Works with standard `Microsoft.Extensions.Logging`

## How It Works

```
Application calls:
logger.LogInformation("User {Email} logged in", customer.Email)
    ↓
RedactingLogger intercepts
    ↓
Scans argument: customer.Email has [PersonalData]
    ↓
Checks [Redaction(Logs=...)]
    ↓
Applies action: OutputRedactionAction.Mask
    ↓
Logs to underlying provider:
"User a****@x.com logged in"
```

## Redaction Actions

- `OutputRedactionAction.None` — Log full value (unsafe unless internal-only logs)
- `OutputRedactionAction.Mask` — Partially masked (email: a****@x.com)
- `OutputRedactionAction.Redact` — Replace with `[REDACTED]`
- `OutputRedactionAction.Pseudonymize` — Replace with pseudonym token
- `OutputRedactionAction.Omit` — Skip the argument entirely (message doesn't include placeholder)

## Usage

### Registration
```csharp
builder.Services.AddSensitiveFlowLogging();
builder.Services.AddLogging(cfg => cfg.AddConsole());
```

### In Application Code
```csharp
[PersonalData]
[Redaction(Logs = OutputRedactionAction.Mask)]
public string Email { get; set; }

// In handler:
logger.LogInformation("Processing order for {Email}", customer.Email);
// Output: "Processing order for a****@x.com"
```

### Default Behavior
If `[Redaction]` not specified for `Logs` context:
- Uses `OutputRedactionAction.None` (logs full value)
- Analyzer SF0001 warns about unmasked sensitive data

To suppress warning, add explicit redaction:
```csharp
[PersonalData]
[Redaction(Logs = OutputRedactionAction.Redact)]
public string InternalNotes { get; set; }
```

## Performance

### Lazy Evaluation
- If log level disabled, no processing occurs
- Reduces overhead for verbose logs in non-debug environments

### Caching
- `SensitiveMemberCache` caches reflection lookups
- Per-type, not per-instance

### Zero-Allocation Mode
- Non-sensitive arguments: pass-through without allocation
- Sensitive arguments: small allocation for masked value
- No regex or complex string parsing

## Masking Strategy

### Email
```
Input:  alice@example.com
Output: a****@example.com
```

### Generic (partial first/last char)
```
Input:  SecretPassword123
Output: S***************3
```

### Null/Empty
```
Input:  null
Output: null (not masked)

Input:  ""
Output: ""
```

## Structured Logging

Works with structured logging properties:

```csharp
using (logger.BeginScope(new Dictionary<string, object>
{
    ["CustomerId"] = customer.Id,
    ["Email"] = customer.Email  // Will be masked if [PersonalData]
}))
{
    logger.LogInformation("Processing customer");
}
```

## Known Limitations

1. **`ILogger<T>` generic detection** — Analyzer SF0001 doesn't detect generic variant
2. **Complex object arguments** — Simple masking strategies; complex redaction requires custom code
3. **Interpolated strings** — `$"Email: {customer.Email}"` not intercepted (use log template instead)

## Possible Improvements

1. **Structured property redaction** — Support for dictionary/bag properties
2. **Audit trail correlation** — Inject audit record ID to log scope
3. **Performance metrics** — Built-in counters for redaction frequency
4. **Custom masking rules** — Per-property masking strategies (phone: ###-###-XXXX)
5. **Log sampling** — Reduce volume of sensitive logs in high-throughput scenarios
