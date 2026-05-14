# EF Core Integration

`SensitiveFlow.EFCore` provides a `SaveChangesInterceptor` that automatically emits `AuditRecord` entries for every sensitive field mutation, without any manual instrumentation.

## How it works

`SensitiveDataAuditInterceptor` hooks into `SavingChangesAsync` (and its synchronous counterpart). Before EF Core commits the changes, it:

1. Filters `ChangeTracker.Entries()` to only entities in `Added`, `Modified`, or `Deleted` state **that have at least one sensitive property** — non-sensitive entities are skipped without allocating a list entry.
2. For each remaining entity, retrieves the cached sensitive property list from `SensitiveMemberCache` (no reflection scan per call).
3. For `Modified` entities, skips properties that were not actually changed (`IsModified == false`).
4. Creates one `AuditRecord` per sensitive field and appends it to `IAuditStore` after `SaveChanges` succeeds.

## Registration

```csharp
// Register your durable IAuditStore, then the interceptor.
builder.Services.AddEfCoreAuditStore<AppDbContext>();
builder.Services.AddSensitiveFlowEFCore();
```

Then wire the interceptor into your `DbContext`:

```csharp
builder.Services.AddDbContext<AppDbContext>((provider, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(provider.GetRequiredService<SensitiveDataAuditInterceptor>());
});
```

## IAuditContext

`IAuditContext` provides the per-request `ActorId` and `IpAddressToken` that the interceptor attaches to every audit record.

By default, `AddSensitiveFlowEFCore` registers `NullAuditContext`, which returns `null` for both properties. This is safe and works without an HTTP pipeline.

### Using the HTTP context

To enrich audit records from ASP.NET Core, register `HttpAuditContext`:

```csharp
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowAspNetCore(); // replaces NullAuditContext with HttpAuditContext
```

### Custom implementation

```csharp
public sealed class MyAuditContext : IAuditContext
{
    private readonly ICurrentUserService _user;

    public MyAuditContext(ICurrentUserService user) => _user = user;

    public string? ActorId => _user.UserId;
    public string? IpAddressToken => null;
}

builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddScoped<IAuditContext, MyAuditContext>();
```

## What gets audited

Only properties explicitly annotated with `[PersonalData]` or `[SensitiveData]` are audited. Public fields without these attributes are silently ignored.

```csharp
public class Order
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;   // audited

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    public string CardToken { get; set; } = string.Empty; // audited

    public string OrderNumber { get; set; } = string.Empty; // NOT audited
}
```

## Operation mapping

| EF Core entity state | AuditOperation |
|----------------------|----------------|
| `Added` | `Create` |
| `Modified` | `Update` |
| `Deleted` | `Delete` |

## Bulk updates and raw SQL

`SensitiveDataAuditInterceptor` only sees writes that go through `SaveChanges`. EF Core's bulk operations translate directly to SQL and bypass the `ChangeTracker`, so they would silently drop audit records on annotated entities. Raw SQL is in the same category.

### Auditing `ExecuteUpdate` and `ExecuteDelete`

Use `ExecuteUpdateAuditedAsync` and `ExecuteDeleteAuditedAsync` from `SensitiveFlow.EFCore.BulkOperations` whenever you need to bulk-mutate entities that carry `[PersonalData]` or `[SensitiveData]`:

```csharp
using SensitiveFlow.EFCore.BulkOperations;

var affected = await db.Customers
    .Where(c => c.Status == "Inactive")
    .ExecuteUpdateAuditedAsync(
        setters => setters.SetProperty(c => c.Email, "redacted@example.com"),
        auditStore,
        auditContext);
```

The helpers issue a single `SELECT` for the affected `DataSubjectId` values, run the bulk modification, and then emit one `AuditRecord` per (subject, annotated field) pair — matching the granularity of a `SaveChanges`-based update.

Setters that target non-annotated columns produce no audit records, so you can mix sensitive and non-sensitive updates in one call without inflating the audit trail. If the entity has no annotated members the helper forwards directly to EF Core with no prefetch.

### Cost guard

The prefetch and audit fan-out are bounded by `SensitiveBulkOperationsOptions.MaxAuditedRows` (default `10_000`). If a single call would touch more subjects, it throws — narrow the predicate, process in batches, or raise the limit explicitly when you know the cost is acceptable.

### Blocking unaudited bulk operations

Register `SensitiveBulkOperationsGuardInterceptor` so that a direct `ExecuteUpdateAsync` / `ExecuteDeleteAsync` against an annotated entity fails fast instead of writing without an audit trail:

```csharp
builder.Services.AddSensitiveBulkOperations();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(
        sp.GetRequiredService<SensitiveDataAuditInterceptor>(),
        sp.GetRequiredService<SensitiveBulkOperationsGuardInterceptor>());
});
```

Set `RequireExplicitAuditing = false` only when bulk operations on annotated entities are audited by a layer in front of EF Core.

### Concurrency considerations

Bulk operations perform a SELECT to prefetch affected `DataSubjectId` values, then execute the modification. Between the prefetch and the execution, concurrent transactions may insert, delete, or modify rows:

- If rows matching the query are **inserted** between prefetch and execution, they will be **modified but not audited**.
- If rows are **deleted** between prefetch and execution, audit records will be **created for subjects that no longer exist**.

For operations where audit completeness is critical, wrap both the prefetch and modification in an explicit transaction:

```csharp
using var txn = await db.Database.BeginTransactionAsync();
try
{
    var affected = await db.Customers
        .Where(c => c.Status == "Inactive")
        .ExecuteUpdateAuditedAsync(
            s => s.SetProperty(c => c.Email, "redacted@example.com"),
            auditStore,
            auditContext);
    
    await txn.CommitAsync();
}
catch
{
    await txn.RollbackAsync();
    throw;
}
```

Bulk operations are typically used in background jobs where concurrency is controlled, so this is usually not a concern. However, if bulk operations are executed in a web context with high concurrency, explicit transaction boundaries ensure audit correctness.

### Raw SQL is still your responsibility

`Database.ExecuteSqlRaw` and `ExecuteSqlInterpolated` are not intercepted: there is no LINQ expression to inspect and no safe way to project the affected subjects. Either avoid raw SQL against entities holding personal data, or emit audit records around it manually.

## NullAuditContext

`NullAuditContext` is a thread-safe singleton (`NullAuditContext.Instance`) that returns `null` for both `ActorId` and `IpAddressToken`. Use it in tests to keep audit records free of HTTP dependencies:

```csharp
var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
```
