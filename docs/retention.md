# Retention

`SensitiveFlow.Retention` provides a lightweight mechanism for declaring and evaluating data retention periods. You control when and how expired data is handled via scheduled jobs or request handlers.

**Important:** Retention evaluation is *manual and explicit* — you must call `RetentionEvaluator` or `RetentionExecutor` on a schedule (e.g., nightly job). The library will not automatically delete data; it only provides evaluation and execution helpers.

## RetentionDataAttribute

Declare the retention window on any property:

```csharp
[RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
public string TaxId { get; set; }

[RetentionData(Years = 1, Months = 6, Policy = RetentionPolicy.DeleteOnExpiration)]
public string SessionToken { get; set; }
```

`Years` and `Months` must be zero or positive. Negative values throw `ArgumentOutOfRangeException` at attribute construction — a negative period would silently produce an expiration in the past, marking every record as already expired.

See [Attributes](attributes.md) for the full property reference.

> **Scope.** `RetentionEvaluator` inspects only the **public instance properties** of the entity you pass in. Properties on nested objects (e.g. `Customer.Address.PostalCode`) are not traversed — call the evaluator on each owned object explicitly.

> **Naming.** `RetentionPolicy.AnonymizeOnExpiration` describes intent. Use `RetentionExecutor` if you want a built-in anonymization pass; otherwise, handlers decide what "anonymize" means and apply it.

## RetentionEvaluator

`RetentionEvaluator` inspects an entity's properties at the time **you** call it — during a scheduled job, a request handler, or a data export. It does not run automatically.

```csharp
// Check whether the entity's retention periods have expired
// relative to the record's creation date
await evaluator.EvaluateAsync(customer, customer.CreatedAt);
```

### With no handlers registered

If a field is expired and no `IRetentionExpirationHandler` is registered, `EvaluateAsync` throws `RetentionExpiredException`:

```csharp
// RetentionExpiredException: field 'TaxId' on 'Customer' expired at 2025-01-15T00:00:00+00:00
```

### With handlers registered

When one or more handlers are registered, expired fields are delivered to every handler instead of throwing:

```csharp
public sealed class AnonymizeOnExpirationHandler : IRetentionExpirationHandler
{
    private readonly IAnonymizer _anonymizer;
    private readonly AppDbContext _db;

    public AnonymizeOnExpirationHandler(IAnonymizer anonymizer, AppDbContext db)
    {
        _anonymizer = anonymizer;
        _db = db;
    }

    public async Task HandleAsync(object entity, string fieldName, DateTimeOffset expiredAt,
        CancellationToken cancellationToken = default)
    {
        var prop = entity.GetType().GetProperty(fieldName);
        if (prop is null) { return; }
        prop.SetValue(entity, _anonymizer.Anonymize(prop.GetValue(entity)?.ToString() ?? string.Empty));
        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

## RetentionExecutor

`RetentionExecutor` is the imperative counterpart to `RetentionEvaluator`. It **mutates** expired fields in place when the policy is `AnonymizeOnExpiration` and returns a report describing the actions taken or required.

```csharp
var executor = new RetentionExecutor();
var report = await executor.ExecuteAsync(customers, c => c.CreatedAt);

Console.WriteLine($"Anonymized fields: {report.AnonymizedFieldCount}");
Console.WriteLine($"Entities pending delete: {report.DeletePendingEntityCount}");
```

Use `RetentionExecutionReport.Entries` to delete or notify for policies the executor does not handle (delete/block/notify). The executor never deletes rows by itself.

## Registration

```csharp
builder.Services.AddRetention();
builder.Services.AddRetentionHandler<AnonymizeOnExpirationHandler>();
builder.Services.AddRetentionExecutor();
```

Multiple handlers can be registered; all are called for each expired field.

## Running the evaluator

**YOU are responsible for running the evaluator** — it does not run automatically. Options include:

### Option 1: Background Service (Always Running)

```csharp
// In a scheduled job or background service
public sealed class RetentionJob : BackgroundService
{
    private readonly RetentionEvaluator _evaluator;
    private readonly AppDbContext _db;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var customers = await _db.Customers.ToListAsync(stoppingToken);
            foreach (var customer in customers)
            {
                try
                {
                    await _evaluator.EvaluateAsync(customer, customer.CreatedAt, stoppingToken);
                }
                catch (RetentionExpiredException ex)
                {
                    // Already logged; handlers are called before this exception
                    // If no handler is registered, you can handle it here
                }
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

### Option 2: On-Demand Trigger

```csharp
// On a specific endpoint or event
public async Task<IResult> ExpireData([FromServices] RetentionEvaluator evaluator, 
    [FromServices] AppDbContext db)
{
    var customers = await db.Customers
        .Where(c => c.CreatedAt < DateTimeOffset.UtcNow.AddYears(-5))
        .ToListAsync();
    
    foreach (var customer in customers)
    {
        await evaluator.EvaluateAsync(customer, customer.CreatedAt);
    }
    
    return Results.Ok($"Evaluated {customers.Count} customers");
}
```

## Calendar accuracy

Retention periods use `DateTimeOffset.AddYears` and `AddMonths` to avoid the drift caused by fixed `TimeSpan` arithmetic on leap years and variable-length months.

```
5 years from 2020-02-29 = 2025-02-28   (not 2025-03-01)
1 month from 2024-01-31 = 2024-02-29   (not 2024-03-02)
```

## Compliance note

Retention policies must be enforced **consistently** across all your data flows:

- ✅ Data subject access: exclude expired data
- ✅ Data export: exclude expired data
- ✅ Batch jobs: run evaluator regularly to process expired records
- ✅ Logging: ensure sensitive fields aren't logged past retention
- ✅ Monitoring: track which records are evaluated and expired
