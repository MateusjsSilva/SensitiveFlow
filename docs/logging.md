# Log Redaction

`SensitiveFlow.Logging` prevents PII from reaching log sinks by intercepting messages before they are written.

## Installation

```bash
dotnet add package SensitiveFlow.Logging
```

## How it works

`RedactingLogger` is an `ILogger` decorator. It wraps any existing `ILogger` and, before forwarding a message, replaces every occurrence of the pattern `[Sensitive]<value>` with a fixed redaction marker.

The pattern matches immediately after the `[Sensitive]` tag up to the first whitespace, comma, or closing brace — enough to redact a structured log parameter value without disturbing the rest of the message template.

## Marking sensitive values

Prefix the log parameter name with `[Sensitive]`:

```csharp
logger.LogInformation("User {[Sensitive]Email} logged in from {Ip}", email, ip);
// Logged as: "User [REDACTED] logged in from 192.168.1.1"
```

Only `[Sensitive]`-tagged values are redacted. Other parameters pass through unchanged.

## Registration

```csharp
builder.Services.AddSensitiveFlowLogging();          // uses [REDACTED] marker
builder.Services.AddSensitiveFlowLogging("***");     // custom marker
```

This registers `DefaultSensitiveValueRedactor` as a singleton `ISensitiveValueRedactor`.

`AddSensitiveFlowLogging()` only registers the redactor. To wrap a concrete logging provider, use the `ILoggingBuilder` overload:

```csharp
builder.Logging.AddSensitiveFlowLogging<ConsoleLoggerProvider>();
```

That registers `RedactingLoggerProvider` alongside the provider you choose to wrap.

### Wrapping a specific logger

```csharp
var redactor = provider.GetRequiredService<ISensitiveValueRedactor>();
var safeLogger = new RedactingLogger(innerLogger, redactor);
```

### Wrapping an entire provider

`RedactingLoggerProvider` wraps every `ILogger` created by an inner provider:

```csharp
var provider = new RedactingLoggerProvider(innerProvider, redactor);
```

## ISensitiveValueRedactor

To customize redaction behavior, implement `ISensitiveValueRedactor`:

```csharp
public interface ISensitiveValueRedactor
{
    string Redact(string value);
}
```

Example — hash-based redaction for deterministic deduplication:

```csharp
public sealed class HashRedactor : ISensitiveValueRedactor
{
    public string Redact(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..8];
    }
}

builder.Services.AddSingleton<ISensitiveValueRedactor, HashRedactor>();
```

## DefaultSensitiveValueRedactor

The built-in implementation replaces any value with a fixed marker string:

```csharp
var redactor = new DefaultSensitiveValueRedactor("[REDACTED]");
redactor.Redact("secret@example.com"); // returns "[REDACTED]"
```

The marker must be a non-whitespace string; an empty or whitespace-only value throws `ArgumentException`.

## OpenTelemetry metrics

`RedactingLogger` emits the `sensitiveflow.redact.fields.count` counter via `System.Diagnostics.Metrics`. Pair it with `SensitiveFlow.Diagnostics` or register the `SensitiveFlow` meter directly:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(SensitiveFlow.Core.Diagnostics.SensitiveFlowDiagnostics.MeterName));
```

## Redaction pattern reference

| Input | Output (default marker) |
|-------|------------------------|
| `User [Sensitive]Email logged in` | `User [REDACTED] logged in` |
| `[Sensitive]Email and [Sensitive]Phone` | `[REDACTED] and [REDACTED]` |
| `No sensitive data here` | `No sensitive data here` |
