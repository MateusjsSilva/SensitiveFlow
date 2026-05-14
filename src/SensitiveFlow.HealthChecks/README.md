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

## Possible Improvements

1. **Policy checks** — Verify retention policies are configured
2. **Performance metrics** — Report audit latency, throughput
3. **Data quality checks** — Flag orphaned records, missing fields
4. **Alerting integration** — PagerDuty, Slack notifications
5. **Audit age tracking** — Warn if audit records > N days old
