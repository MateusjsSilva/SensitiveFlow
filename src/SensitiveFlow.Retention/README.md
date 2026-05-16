# SensitiveFlow.Retention

Automatic data retention and expiration policies to manage data lifecycle.

## Main Components

### Retention Scheduler
- **`RetentionScheduler`** — Scheduled background job for data cleanup
  - Deletes personal/sensitive data based on retention policy
  - Logs deletions to audit trail
  - Supports cron expressions for scheduling
  - Can run in-process or as separate service

### Retention Policies
- **`IRetentionPolicy`** — Defines what data expires when
  - `Entity`: Type to apply policy to
  - `RetentionDays`: How long to keep data
  - `Condition`: Optional predicate (e.g., inactive users)
  - `Action`: Delete, anonymize, or archive

### Configuration
- **`RetentionSchedulerOptions`** — Settings for scheduler
  - `CronExpression`: When to run (default: daily at 2 AM)
  - `TimeZone`: Scheduler timezone
  - `DryRun`: Test mode (logs what would be deleted)
  - `BatchSize`: Records deleted per batch
  - `MaxDurationMinutes`: Timeout for single run

## How It Works

### Scheduled Execution
```
Cron trigger at 2 AM
    ↓
RetentionScheduler starts
    ↓
For each IRetentionPolicy:
    ↓
    Query entities matching retention date
    ↓
    If condition matches: apply action (delete/anonymize)
    ↓
    Log to audit trail
    ↓
Notify via IRetentionCallback
```

### Deletion Flow
```
Customer created: 2023-01-01
Retention policy: 365 days
Current date: 2024-02-01
    ↓
Eligible for deletion (> 365 days)
    ↓
Execute DELETE or UPDATE (anonymize)
    ↓
Audit record created: Delete operation
    ↓
Callback notified (e.g., email, webhook)
```

## Usage

### Registration
```csharp
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowRetention();
```

### Define Policy
```csharp
public sealed class CustomerRetentionPolicy : IRetentionPolicy
{
    public Type Entity => typeof(Customer);
    public int RetentionDays => 365;  // Keep 1 year
    public string CronExpression => "0 2 * * *";  // Daily at 2 AM
    
    public Expression<Func<Customer, bool>>? Condition =>
        c => c.Status == "Inactive";  // Only inactive customers
    
    public RetentionAction Action => RetentionAction.HardDelete;
}

builder.Services.AddScoped<IRetentionPolicy, CustomerRetentionPolicy>();
```

### Manual Trigger
```csharp
public sealed class DataController : ControllerBase
{
    private readonly RetentionScheduler _scheduler;

    [HttpPost("admin/run-retention")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RunRetention()
    {
        var result = await _scheduler.ExecuteAsync();
        return Ok(new
        {
            recordsDeleted = result.RecordsDeleted,
            duration = result.Duration,
            errors = result.Errors
        });
    }
}
```

### Dry Run (Test)
```csharp
var options = new RetentionSchedulerOptions
{
    DryRun = true  // Log what would be deleted, don't delete
};

var result = await scheduler.ExecuteAsync(options);
Console.WriteLine($"Would delete: {result.RecordsDeleted} records");
```

## Retention Actions

### Hard Delete (Default)
```csharp
Action = RetentionAction.HardDelete
// DELETE FROM Customers WHERE ...
```

**Pros**: Complete removal, regulatory compliance
**Cons**: Cannot recover, breaks referential integrity

### Logical Delete
```csharp
Action = RetentionAction.LogicalDelete
// UPDATE Customers SET IsDeleted = true WHERE ...
```

**Pros**: Recoverable, keeps audit trail
**Cons**: Doesn't reduce storage, needs filtering in queries

### Anonymize
```csharp
Action = RetentionAction.Anonymize
// UPDATE Customers SET Email = NULL, Name = 'ANONYMIZED' WHERE ...
```

**Pros**: Keeps statistics, regulatory compliant
**Cons**: Need to verify truly anonymized

### Archive
```csharp
Action = RetentionAction.Archive
// INSERT INTO Archive SELECT * FROM Customers WHERE ...
// DELETE FROM Customers WHERE ...
```

**Pros**: Keeps data accessible, frees hot storage
**Cons**: Complex two-step process

## Configuration

### Scheduling
```csharp
var options = new RetentionSchedulerOptions
{
    CronExpression = "0 2 * * *",  // Daily 2 AM
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"),
    DryRun = false,
    BatchSize = 1000,  // Delete 1000 at a time
    MaxDurationMinutes = 60
};
```

### Callbacks
```csharp
public sealed class MyRetentionCallback : IRetentionCallback
{
    private readonly ILogger<MyRetentionCallback> _logger;

    public MyRetentionCallback(ILogger<MyRetentionCallback> logger)
        => _logger = logger;

    public async Task OnCompleted(RetentionResult result)
    {
        _logger.LogInformation(
            "Retention completed: {Deleted} records in {Duration}ms",
            result.RecordsDeleted, result.Duration.TotalMilliseconds
        );

        if (result.Errors.Any())
        {
            // Email admin about failures
            await SendAlertAsync(result.Errors);
        }
    }
}

builder.Services.AddScoped<IRetentionCallback, MyRetentionCallback>();
```

## Data Lifecycle Management

### Storage Minimization
- Keep data only as long as necessary
- Retention policies enforce data minimization
- Audit trail provides accountability

### Purpose-Based Retention
- Don't keep data beyond its business purpose
- Retention trigger based on business logic
- Transparent and auditable policies

### Deletion Accountability
- Deletions logged to immutable audit trail
- Complete record of what was deleted and when
- Demonstrates responsible data handling

## Safety Features

### Dry Run Mode
Test before production deletion:
```csharp
options.DryRun = true;
await scheduler.ExecuteAsync(options);
// Reviews logs without deleting
```

### Batch Processing
```csharp
BatchSize = 500  // Don't lock table too long
```

### Timeout Protection
```csharp
MaxDurationMinutes = 60  // Kill job if runs > 1 hour
```

### Condition Validation
```csharp
Condition = c => c.Status == "Inactive" && c.LastLogin < deletionDate
// Multiple predicates prevent accidental deletion
```

## Advanced Features

### Incremental Scheduling
Track the last successful run per policy to avoid reprocessing the same data:
```csharp
var tracker = new RetentionRunTracker();
var lastRun = tracker.GetLastRunAt("policy1");

if (lastRun == null || DateTimeOffset.UtcNow - lastRun > TimeSpan.FromHours(24))
{
    await scheduler.ExecuteAsync();
    tracker.MarkRanAt("policy1", DateTimeOffset.UtcNow);
}
```

### Parallel Policy Execution
Run multiple independent retention batches concurrently:
```csharp
var executor = new ParallelRetentionExecutor();
var batches = new[]
{
    new RetentionBatch(customers, c => ((Customer)c).CreatedAt),
    new RetentionBatch(orders, o => ((Order)o).CreatedAt)
};

var report = await executor.ExecuteParallelAsync(batches);
Console.WriteLine($"Processed {report.Entries.Count} entries in parallel");
```

### Retention Analytics
Collect and analyze retention execution metrics:
```csharp
var collector = new RetentionAnalyticsCollector();

// Record each run
collector.RecordRun(report, DateTimeOffset.UtcNow, durationMs: 1250);

// Get execution history
var history = collector.GetRunHistory();

// Analyze trends
var summary = collector.GetTrendSummary();
Console.WriteLine($"Total runs: {summary.TotalRuns}");
Console.WriteLine($"Total anonymized: {summary.TotalAnonymized}");
Console.WriteLine($"Average per run: {summary.AverageAnonymizedPerRun}");
```

### Selective Re-anonymization
Re-anonymize entities matching a condition without waiting for retention expiration:
```csharp
var reAnon = new RetentionReAnonymizer();

// Re-anonymize all customers with a specific attribute
var result = await reAnon.ReAnonymizeAsync<Customer>(
    customers,
    c => c.NeedsReAnonymization == true
);

Console.WriteLine($"Re-anonymized {result.AnonymizedFieldCount} fields");
```

### Archive Tiering
Manage cold storage of archived entities:
```csharp
var archive = new InMemoryRetentionArchiveProvider();

// Archive expired entities
await archive.ArchiveAsync(expiredCustomers, archiveKey: "customers-2024");

// Retrieve when needed
var archived = await archive.RetrieveAsync("customers-2024");

// List all archives
var keys = await archive.ListArchiveKeysAsync();
```

### Notification Templates
Configure alerts for retention completion events:
```csharp
var template = new RetentionNotificationTemplate
{
    Subject = "Retention Run Completed",
    Body = "Anonymized {AnonymizedCount} fields, {DeletePendingCount} entities pending deletion",
    Channel = RetentionNotificationChannel.Email
};

var message = template.Format(report);
```

### Retention Analytics Reporting
Generate formatted reports from retention metrics:
```csharp
var collector = new RetentionAnalyticsCollector();
// ... record runs ...

var summary = collector.GetTrendSummary();

// Generate text report
var textReport = RetentionReportGenerator.GenerateTextReport(summary);
Console.WriteLine(textReport);

// Generate CSV for export
var csvReport = RetentionReportGenerator.GenerateCsvReport(collector.GetRunHistory());

// Generate JSON for APIs
var jsonReport = RetentionReportGenerator.GenerateJsonReport(summary);

