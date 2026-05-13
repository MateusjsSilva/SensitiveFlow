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

The interceptor observes EF Core's `ChangeTracker` during `SaveChanges` / `SaveChangesAsync`. Operations that bypass tracked entities do not produce per-field audit records automatically:

- `ExecuteUpdate` / `ExecuteUpdateAsync`
- `ExecuteDelete` / `ExecuteDeleteAsync`
- `Database.ExecuteSqlRaw` / `ExecuteSqlInterpolated`
- direct database changes outside EF Core

For these paths, create audit records explicitly around the bulk operation, or prefer loading and modifying tracked entities when per-field audit is required.

## NullAuditContext

`NullAuditContext` is a thread-safe singleton (`NullAuditContext.Instance`) that returns `null` for both `ActorId` and `IpAddressToken`. Use it in tests to keep audit records free of HTTP dependencies:

```csharp
var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
```
