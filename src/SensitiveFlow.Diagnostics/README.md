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

### 1. Configuration Validation at Startup

Validate SensitiveFlow configuration before the app starts:

```csharp
var services = builder.Services.BuildServiceProvider();

// Add validation service
builder.Services.AddSensitiveFlowValidation(options =>
{
    options.RequireAuditStore = true;           // Audit store required
    options.RequireTokenStore = true;           // Token store for pseudonymization
    options.RequireJsonRedaction = true;        // JSON redaction configured
    options.RequireRetention = true;            // Retention policies active
    options.FailOnWarning = false;              // Warnings don't block startup
});

// In Program.cs after building host:
var app = builder.Build();
var validator = app.Services.GetRequiredService<SensitiveFlowConfigurationValidator>();
var report = validator.Validate(app.Services);

if (!report.IsValid)
{
    foreach (var diagnostic in report.Diagnostics)
    {
        var icon = diagnostic.IsError ? "❌" : "⚠️";
        Console.WriteLine($"{icon} {diagnostic.Code}: {diagnostic.Message}");
    }
    
    if (report.HasErrors)
        throw new InvalidOperationException("Configuration validation failed");
}

app.Run();
```

**Validation Codes:**
- `SF-CONFIG-001` — No IAuditStore registered (Error if required)
- `SF-CONFIG-002` — No ITokenStore registered (Error if required)
- `SF-CONFIG-003` — IPseudonymizer without ITokenStore (Warning)
- `SF-CONFIG-004` — JSON redaction missing (Warning if required)
- `SF-CONFIG-005` — Retention not configured (Warning if required)
- `SF-CONFIG-009` — EF Core interceptor without audit store (Warning)

### 2. OpenTelemetry Integration

Export SensitiveFlow metrics and traces to observability platforms:

```csharp
// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(SensitiveFlowDiagnostics.MeterName)
            .AddConsoleExporter()
            // Or send to Prometheus, Jaeger, etc.
            .AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(SensitiveFlowDiagnostics.ActivitySourceName)
            .AddConsoleExporter()
            // Or send to Jaeger, DataDog, etc.
            .AddJaegerExporter();
    });

// Instrument the audit store
builder.Services.Decorate<IAuditStore, InstrumentedAuditStore>();
```

**Metrics Exported:**
```
sensitiveflow.audit.append.duration (Histogram)
    unit: ms
    tags: entity, field, operation, batch.size
    description: Time spent in audit append operations

sensitiveflow.audit.append.count (Counter)
    unit: records
    tags: entity, operation
    description: Number of audit records appended
```

**Example: View Metrics in Prometheus**
```bash
# Query: rate(sensitiveflow_audit_append_duration[1m])
# Returns: Average audit latency per minute

# Query: rate(sensitiveflow_audit_append_count[1m])
# Returns: Throughput (records/minute)
```

**Example: Traces in Jaeger**
```
Trace: sensitiveflow.audit.append
├─ operation: Create
├─ entity: Customer
├─ field: Email
├─ duration: 2.5ms
└─ tags:
    audit.entity=Customer
    audit.operation=Create
    audit.field=Email
```

### 3. Distributed Tracing Across Services

Track audit operations across microservices:

```csharp
// Service 1: Web API (creates customer)
[HttpPost("customers")]
public async Task<IActionResult> CreateCustomer(CreateCustomerRequest req)
{
    // SensitiveDataAuditInterceptor automatically:
    // - Creates Activity "sensitiveflow.audit.append"
    // - Propagates trace context to database
    // - Tags with entity, operation, field
    
    var customer = new Customer { Email = req.Email };
    await db.Customers.AddAsync(customer);
    await db.SaveChangesAsync();  // Audit recorded with trace context
    
    return CreatedAtAction(nameof(GetCustomer), customer);
}

// Service 2: Audit Processor (background job)
public async Task ProcessAuditRecords()
{
    var records = await auditStore.QueryStreamAsync(
        new AuditQuery().InTimeRange(start, end));
    
    await foreach (var record in records)
    {
        // Activity created here is linked to original via trace context
        using var activity = ActivitySource.StartActivity(
            "sensitiveflow.audit.process",
            ActivityKind.Internal);
        
        activity?.SetTag("audit.id", record.Id);
        await ProcessRecord(record);
    }
}

// In Jaeger UI:
// Trace spans for:
// 1. CreateCustomer (Service 1)
// 2. sensitiveflow.audit.append (Service 1 DB)
// 3. sensitiveflow.audit.process (Service 2)
// All linked via trace context
```

### 4. Performance Profiling

Measure redaction performance:

```csharp
// Manual profiling
var stopwatch = Stopwatch.StartNew();

for (int i = 0; i < 10_000; i++)
{
    var masked = customer.Email.MaskEmail();
}

stopwatch.Stop();
Console.WriteLine($"Redacted 10,000 emails in {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Throughput: {10_000 / stopwatch.Elapsed.TotalSeconds:F0} ops/sec");

// OpenTelemetry metrics (automatic)
// Query: histogram_quantile(0.95, rate(sensitiveflow_audit_append_duration[5m]))
// Returns: 95th percentile append latency
```

### 5. Compliance Checks

Automated compliance validation:

```csharp
// Validate DataSubjectId on all sensitive entities
var validator = new SensitiveFlowConfigurationValidator(
    new SensitiveFlowValidationOptions
    {
        RequireAuditStore = true,
        RequireRetention = true,  // Ensure retention policies active
        FailOnWarning = true      // Strict mode for compliance
    });

var report = validator.Validate(app.Services);

// Log diagnostic results
if (!report.IsValid)
{
    logger.LogError("Configuration compliance check failed:");
    foreach (var diagnostic in report.Diagnostics)
    {
        if (diagnostic.IsError)
            logger.LogError("❌ {Code}: {Message}", diagnostic.Code, diagnostic.Message);
        else
            logger.LogWarning("⚠️ {Code}: {Message}", diagnostic.Code, diagnostic.Message);
    }
}

// Assert in CI/CD
Assert.True(report.IsValid, report.ToString());
```

### 6. Health Check Integration

Expose diagnostics via health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<SensitiveFlowDiagnosticsHealthCheck>("sensitiveflow-diagnostics");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// GET /health response:
// {
//   "status": "Healthy",
//   "checks": {
//     "sensitiveflow-diagnostics": {
//       "status": "Healthy",
//       "description": "Audit store: connected, 1,234 records; Retention: active; JSON redaction: enabled"
//     }
//   }
// }
```

## Instrumentation Points

### Audit Append (Hot Path)
```
Request → SaveChanges
    ↓
Activity: "sensitiveflow.audit.append" starts
    ↓
Tags: entity, field, operation, batch.size
    ↓
Append to store
    ↓
Metrics: duration, count
    ↓
Activity ends
```

### Audit Query (Cold Path)
Not instrumented (not on hot path). Add custom instrumentation if needed:

```csharp
using var activity = ActivitySource.StartActivity("sensitiveflow.audit.query");
activity?.SetTag("query.entity", query.Entity);
var records = await store.QueryAsync(query);
```

## Best Practices

### Metrics Collection
1. **Set up Prometheus scraping** for metrics collection
2. **Configure alerts** for high append latency (>50ms p95)
3. **Monitor throughput** to detect performance regressions
4. **Track outliers** for slow audit operations

### Tracing
1. **Enable trace sampling** (e.g., 10% in production)
2. **Export to Jaeger or DataDog** for visualization
3. **Correlate with logs** using trace IDs
4. **Set retention** (e.g., 7 days) for audit traces

### Validation
1. **Run at application startup** (fail-fast)
2. **Use strict mode** in CI/CD pipelines
3. **Log diagnostics** for troubleshooting
4. **Update policies** when configuration changes

## Advanced Features

### Custom Metrics

Track domain-specific metrics beyond audit operations:

```csharp
var customMetrics = new CustomMetricsProvider();

// Record sensitive field access
customMetrics.RecordSensitiveFieldAccess("Email", "Customer");

// Record redaction duration
customMetrics.RecordRedactionDuration(durationMs: 2.5, redactionKind: "Mask");

// Record compliance violations
customMetrics.RecordComplianceViolation("UnauthorizedAccess", "User accessed non-owned data");
```

**Available Metrics:**
- `sensitiveflow.sensitive_fields_accessed` — Sensitive field accesses (with field name, entity tags)
- `sensitiveflow.redaction.duration` — Redaction operation times (with kind tag)
- `sensitiveflow.compliance_violations` — Compliance violation counts

### Metric Aggregations

Pre-compute percentiles and statistics:

```csharp
var aggregation = new MetricAggregationService();

// Record measurements
aggregation.Record("audit.latency", 25.5);
aggregation.Record("audit.latency", 30.2);
aggregation.Record("audit.latency", 55.0);

// Query aggregations
var p95 = aggregation.GetPercentile("audit.latency", percentile: 95);        // 50.85
var avg = aggregation.GetAverage("audit.latency");                            // 36.9
var (min, max, count, mean) = aggregation.GetStatistics("audit.latency");    // (25.5, 55, 3, 36.9)
```

### Alert Rules

Pre-built alert rule templates for common scenarios:

```csharp
// Get all alert templates
var templates = AlertRuleTemplates.GetAllTemplates();

foreach (var rule in templates)
{
    Console.WriteLine($"{rule.Name}: {rule.Description}");
    Console.WriteLine($"  Query: {rule.Query}");
    Console.WriteLine($"  Severity: {rule.Severity}");
}

// Export as Prometheus alert rules
var prometheusRules = templates.Select(r => new
{
    alert = r.Name,
    expr = r.Query,
    for = r.For.ToString(@"hh\:mm\:ss"),
    annotations = r.Annotations
});
```

**Built-in Templates:**
- `HighAuditLatency` — p95 > 50ms
- `BulkDeleteDetected` — >50 deletes in 1 hour
- `AuditThroughputDrop` — <10 records/min
- `ComplianceViolation` — Any violation detected
- `SlowRedaction` — Redaction p95 > 5ms
- `SensitiveFieldAccessSpike` — >1000 accesses/min

### Compliance Reporting

Generate automated compliance reports:

```csharp
var reportService = new ComplianceReportService(auditStore);

// Audit frequency report
var frequency = await reportService.GenerateAuditFrequencyReportAsync(
    startDate: DateTime.Now.AddMonths(-1),
    endDate: DateTime.Now);

Console.WriteLine($"Total records: {frequency.TotalAuditRecords}");
Console.WriteLine($"Daily average: {frequency.DailyAverage}");
Console.WriteLine($"Top operations: {string.Join(", ", frequency.OperationCounts)}");

// Data subject coverage report
var coverage = await reportService.GenerateDataSubjectCoverageReportAsync(start, end);
Console.WriteLine($"Unique subjects: {coverage.UniqueDataSubjects}");
Console.WriteLine($"Modification audit coverage: {coverage.CoveragePercentage:F1}%");

// Retention compliance report
var retention = await reportService.GenerateRetentionComplianceReportAsync(
    retentionDays: 365);
Console.WriteLine($"Eligible for deletion: {retention.SubjectsEligibleForDeletion}");
Console.WriteLine($"Compliance: {retention.CompliancePercentage:F1}%");
```

### Performance Baselines

Compare metrics against expected thresholds:

```csharp
var baselines = new PerformanceBaselineService();

// Define baselines
baselines.DefineBaseline("audit.append.duration", new PerformanceBaseline
{
    Target = 10.0,  // 10ms target
    WarningThreshold = 20,  // 20% deviation = warning
    CriticalThreshold = 50  // 50% deviation = critical
});

// Check current performance
var result = baselines.CheckBaseline("audit.append.duration", currentValue: 12.5);

Console.WriteLine($"Status: {result.Status}");  // Warning
Console.WriteLine($"Deviation: {result.Deviation:+0.0;-0.0}%");  // +25.0%
Console.WriteLine($"Recommendation: {result.Recommendation}");
```

### Query Optimization Advisor

Track query patterns and get index recommendations:

```csharp
var advisor = new QueryOptimizationAdvisor();

// Record queries as they execute
// (typically done via interceptor or middleware)
advisor.RecordQueryPattern(entity: "Customer", operation: null, dataSubjectId: "user-123");

// Get recommendations
var recommendations = advisor.GetIndexRecommendations();

foreach (var rec in recommendations.OrderByDescending(r => r.Priority))
{
    Console.WriteLine($"{rec.Priority}: {rec.Reason}");
    Console.WriteLine($"  {rec}");
    Console.WriteLine($"  Expected improvement: {rec.EstimatedImprovement}");
}

// SQL output:
// CREATE INDEX IX_Customer_DataSubjectId_Timestamp_DESC 
// ON Customer (DataSubjectId, Timestamp DESC)
```

**Recommendations by Pattern:**
- `DataSubjectId + Timestamp` → High priority (most common query)
- `Entity + Operation + Timestamp` → Medium priority
- `ActorId + Timestamp` → Medium priority (compliance)
