# SensitiveFlow.Diagnostics

Diagnostic utilities for runtime performance profiling and compliance verification.

## Main Components

### Performance Profiling
- **`RedactionPerformanceProfiler`** — Measures redaction overhead
  - Tracks time spent in redaction operations
  - Reports throughput metrics
  - Identifies bottlenecks

### Compliance Verification
- **`DataSubjectIdValidator`** — Validates entities have proper identifiers
- **`PolicyValidator`** — Verifies retention policies are correct

## Usage

### Profiling Redaction Performance
```csharp
var profiler = new RedactionPerformanceProfiler();
profiler.StartMeasure("email-redaction");

// ... perform redaction ...

var result = profiler.StopMeasure("email-redaction");
Console.WriteLine($"Redacted 1000 emails in {result.ElapsedMilliseconds}ms");
```

## Possible Improvements

1. **OpenTelemetry integration** — Export metrics to observability platforms
2. **Distributed tracing** — Track audit operations across services
3. **Policy compliance checks** — Automated compliance validation
