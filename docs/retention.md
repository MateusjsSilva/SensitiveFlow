# Retention

`SensitiveFlow.Retention` provides a lightweight mechanism for declaring and evaluating data retention periods without running background jobs or hidden timers.

## RetentionDataAttribute

Declare the retention window on any property:

```csharp
[RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
public string TaxId { get; set; }

[RetentionData(Years = 1, Months = 6, Policy = RetentionPolicy.DeleteOnExpiration)]
public string SessionToken { get; set; }
```

See [Attributes](attributes.md) for the full property reference.

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

## Registration

```csharp
builder.Services.AddRetention();
builder.Services.AddRetentionHandler<AnonymizeOnExpirationHandler>();
```

Multiple handlers can be registered; all are called for each expired field.

## Running the evaluator

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
                await _evaluator.EvaluateAsync(customer, customer.CreatedAt, stoppingToken);
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

## Calendar accuracy

Retention periods use `DateTimeOffset.AddYears` and `AddMonths` to avoid the drift caused by fixed `TimeSpan` arithmetic on leap years and variable-length months.

```
5 years from 2020-02-29 = 2025-02-28   (not 2025-03-01)
1 month from 2024-01-31 = 2024-02-29   (not 2024-03-02)
```
