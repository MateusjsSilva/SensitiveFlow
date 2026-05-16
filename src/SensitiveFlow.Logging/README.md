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

## Advanced Features

### 1. Structured Property Redaction

Redact sensitive keys in structured log scopes and dictionary properties:

```csharp
var redactor = new StructuredPropertyRedactor(
    new[] { "ApiKey", "Password" },
    redactedPlaceholder: "[REDACTED]");

var options = new SensitiveLoggingOptions { StructuredPropertyRedactor = redactor };
builder.Services.AddSensitiveFlowLogging(opts => opts.StructuredPropertyRedactor = redactor);

// Usage:
using (logger.BeginScope(new Dictionary<string, object>
{
    ["ApiKey"] = "secret-key-123",  // Will be redacted
    ["UserId"] = "user-456"          // Not redacted
}))
{
    logger.LogInformation("Processing request");
}
```

### 2. Audit Trail Correlation

Automatically inject correlation IDs into log scopes from `SensitiveFlowCorrelation.Current`:

```csharp
public sealed class AuditCorrelationScope : ILogger
{
    // Injects AuditCorrelationId, AuditRequestId, AuditTraceId into all log scopes
}

// Usage:
var innerLogger = loggerProvider.CreateLogger("MyApp");
var correlationLogger = new AuditCorrelationScope(innerLogger);

SensitiveFlowCorrelation.Current = new AuditCorrelationSnapshot
{
    CorrelationId = Guid.NewGuid()
};

correlationLogger.LogInformation("Operation started");
// Log now includes {AuditCorrelationId: ...} in structured properties
```

### 3. Redaction Performance Metrics

Track redaction operations via OpenTelemetry metrics:

```csharp
var collector = new RedactionMetricsCollector();

collector.RecordRedaction("Email", "Mask");
collector.RecordRedaction("Password", "Redact");
collector.RecordMessageScanned();
collector.RecordRedactionDuration(1.5);

// Metrics exported:
// - sensitiveflow_log_redaction_total{field_name, action}
// - sensitiveflow_log_messages_scanned_total
// - sensitiveflow_log_redaction_duration_ms histogram
```

### 4. Custom Masking Rules

Pluggable masking strategies for flexible field masking:

```csharp
var registry = new MaskingStrategyRegistry();

// Built-in strategies
registry.GetStrategy("phone")?.Mask("555-1234-5678");      // ***-***-**78
registry.GetStrategy("creditcard")?.Mask("4532015112830366"); // ****-****-****-0366
registry.GetStrategy("ipaddress")?.Mask("192.168.1.100");   // ***.***.1.100

// Custom strategy
registry.Register("custom", new CustomMaskingStrategy());
var strategy = registry.GetStrategy("custom");
```

### 5. Log Sampling

Reduce log volume for high-throughput scenarios containing sensitive data:

```csharp
var sampler = new LogSamplingFilter(samplingRate: 0.1); // Log 10% of sensitive entries

// Non-sensitive logs always logged
Assert.True(sampler.ShouldLog(hasRedactedFields: false)); // Always true

// Sensitive logs sampled
Assert.True(sampler.SamplingRate < 1.0);        // Sampling enabled
Assert.True(sampler.IsEnabled);                 // Sampling active

// Integration with options:
var options = new SensitiveLoggingOptions 
{ 
    SamplingFilter = new LogSamplingFilter(0.5)
};
```
