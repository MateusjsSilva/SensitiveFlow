# SensitiveFlow.HealthChecks

Health checks for sensitive data protection and compliance verification.

## Main Components

### Health Check Providers
- **`SensitiveFlowHealthCheck`** — Verifies audit store connectivity
- **`DataSubjectExportHealthCheck`** — Validates data export functionality
- **`RetentionPolicyHealthCheck`** — Confirms retention scheduler health

## Available Checks

### Audit Store Health
Verifies the audit storage is accessible and responsive:

```csharp
builder.Services.AddHealthChecks()
    .AddSensitiveFlowAudit<EfCoreAuditStore<AppDbContext>>();
```

**Checks**:
- Can connect to audit storage
- Can read recent records
- Can insert test record (if enabled)
- Response time acceptable

### Data Export Health
Ensures data export pipeline works:

```csharp
builder.Services.AddHealthChecks()
    .AddSensitiveFlowDataSubjectExport();
```

**Checks**:
- Can query test subject
- Can serialize export data
- File system writable (if file-based)

### Retention Policy Health
Validates retention scheduler configuration:

```csharp
builder.Services.AddHealthChecks()
    .AddSensitiveFlowRetention();
```

**Checks**:
- Policies are registered
- Database accessible
- Last run completed successfully
- No overdue policies

## Usage

### Complete Setup
```csharp
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowRetention();
builder.Services.AddSensitiveFlowAnonymization();

builder.Services.AddHealthChecks()
    .AddSensitiveFlowAudit<EfCoreAuditStore<AppDbContext>>()
    .AddSensitiveFlowDataSubjectExport()
    .AddSensitiveFlowRetention();

app.MapHealthChecks("/health");
```

### Health Endpoint Response
```json
{
  "status": "Healthy",
  "checks": {
    "SensitiveFlow.Audit": {
      "status": "Healthy",
      "description": "Audit store connected, 1,234 records"
    },
    "SensitiveFlow.Retention": {
      "status": "Healthy",
      "description": "Last run: 2 hours ago, 42 records deleted"
    },
    "SensitiveFlow.Export": {
      "status": "Healthy",
      "description": "Export service ready"
    }
  }
}
```

## Custom Health Checks

### Create Custom Check
```csharp
public sealed class SensitiveDataCoverageDCheck : IHealthCheck
{
    private readonly DbContext _dbContext;

    public SensitiveDataCoverageDCheck(DbContext dbContext) => _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var unannotatedCount = await _dbContext.Customers
            .AsNoTracking()
            .CountAsync(c => string.IsNullOrEmpty(c.DataSubjectId), cancellationToken);

        if (unannotatedCount > 0)
        {
            return HealthCheckResult.Unhealthy(
                $"Found {unannotatedCount} records without DataSubjectId"
            );
        }

        return HealthCheckResult.Healthy("All customers have DataSubjectId");
    }
}

builder.Services.AddHealthChecks()
    .AddCheck<SensitiveDataCoverageCheck>("SensitiveFlow.Coverage");
```

## Monitoring

### K8s Liveness/Readiness
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 30

readinessProbe:
  httpGet:
    path: /health/ready
    port: 5000
  initialDelaySeconds: 5
  periodSeconds: 10
```

### Grafana Alerting
```grafana
alert: AuditStoreFailing
  expr: health_status{check="SensitiveFlow.Audit"} == 0
  for: 5m
  annotations:
    summary: "Audit store is unhealthy"
```

## Advanced Features

### Retention Policy Validation
Verify that retention policies are properly configured:

```csharp
var validator = sp.GetRequiredService<RetentionPolicyValidator>();
var result = validator.Validate();

if (!result.IsValid)
{
    foreach (var issue in result.Issues)
    {
        Console.WriteLine($"Policy issue: {issue}");
    }
}
```

**Components:**
- `RetentionPolicyValidator` — Validates policy configuration
- `PolicyValidationResult` — Detailed validation results with issues list

### Performance Metrics Reporting
Track audit latency, throughput, and health check success rates:

```csharp
var collector = sp.GetRequiredService<HealthCheckPerformanceCollector>();

// Record operations
collector.RecordHealthCheck("Audit", elapsedMilliseconds: 150, success: true);
collector.RecordAuditOperation("Read", recordCount: 500, elapsedMilliseconds: 200);

// Query metrics
var slowChecks = collector.GetSlowChecks(thresholdMs: 1000);
var throughput = collector.GetAuditThroughputRecordsPerSec();
var avgLatency = collector.GetAverageLatencyMs();
```

**Components:**
- `HealthCheckPerformanceCollector` — Aggregates performance metrics
- `PerformanceMetric` — Per-check statistics (count, latency, success rate)

### Data Quality Checking
Detect orphaned records, missing required fields, and duplicates:

```csharp
var checker = sp.GetRequiredService<DataQualityChecker>();

// Check for missing fields
var result = await checker.CheckForMissingFieldsAsync(
    "Customer", 
    requiredFields: new[] { "DataSubjectId", "Email" }
);

if (!result.IsHealthy)
{
    // Handle quality issues
}
```

**Components:**
- `DataQualityChecker` — Entity-level data validation
- `DataQualityResult` — Validation results with issue details

### Alerting Integration
Configure alerts for health check failures with webhook, Slack, or PagerDuty:

```csharp
builder.Services.AddSensitiveFlowHealthAlerting(options =>
{
    options.AddRule("Audit", AlertSeverity.Critical, 
        webhookUrl: "https://alerts.example.com/webhook");
    
    options.AddRule("Retention", AlertSeverity.Warning,
        slackChannel: "#alerts");
});
```

**Components:**
- `HealthAlertingPolicy` — Rule registry and management
- `AlertingRule` — Per-check alert configuration (webhook, Slack, PagerDuty)
- `AlertSeverity` — Alert levels (Info, Warning, Error, Critical)

### Audit Age Tracking
Monitor audit record age and receive recommendations:

```csharp
var tracker = sp.GetRequiredService<AuditAgeTracker>();
tracker.WarningDaysThreshold = 30;
tracker.CriticalDaysThreshold = 90;

// Analyze oldest record
var analysis = tracker.Analyze(oldestRecordDate);

if (analysis.Status == AuditAgeStatus.Warning)
{
    var recommendations = tracker.GetRecommendations(analysis);
    // Review recommendations and implement archival
}
```

**Components:**
- `AuditAgeTracker` — Age analysis and recommendations
- `AuditAgeAnalysis` — Per-category age breakdown with recommendations
- `AuditAgeStatus` — Health status (Healthy, Warning, Critical)
