# Diagnostics

`SensitiveFlow.Diagnostics` bridges the library to OpenTelemetry by emitting spans and metrics through `System.Diagnostics`.

## Installation

```bash
dotnet add package SensitiveFlow.Diagnostics
```

## What it emits

- ActivitySource name: `SensitiveFlow` (`SensitiveFlowDiagnostics.ActivitySourceName`)
- Meter name: `SensitiveFlow` (`SensitiveFlowDiagnostics.MeterName`)
- Span: `sensitiveflow.audit.append`
- Metrics:
  - `sensitiveflow.audit.append.duration` (histogram, ms)
  - `sensitiveflow.audit.append.count` (counter, records)
  - `sensitiveflow.redact.fields.count` (counter, fields)
  - `sensitiveflow.audit.buffer.pending` (gauge, items) — requires `BufferedAuditStore`
  - `sensitiveflow.audit.buffer.dropped` (counter, items) — requires `BufferedAuditStore`
  - `sensitiveflow.audit.buffer.flush_failures` (counter, failures) — requires `BufferedAuditStore`

## Registration

```csharp
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Diagnostics.Extensions;

builder.Services.AddSensitiveFlowDiagnostics();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(SensitiveFlowDiagnostics.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(SensitiveFlowDiagnostics.MeterName));
```

## Notes

- The diagnostics decorator wraps the registered `IAuditStore`. Apply it after `AddAuditStore<T>()`.
- Retry wrappers can be placed before or after the diagnostics decorator depending on whether you want one span per attempt or one span for the entire retry window.
