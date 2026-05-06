# EF Core Integration

`SensitiveFlow.EFCore` provides a `SaveChangesInterceptor` that automatically emits `AuditRecord` entries for every sensitive field mutation, without any manual instrumentation.

## How it works

`SensitiveDataAuditInterceptor` hooks into `SavingChangesAsync` (and its synchronous counterpart). Before EF Core commits the changes, it:

1. Iterates over all entities in `Added`, `Modified`, or `Deleted` state.
2. For each entity, scans its properties for `[PersonalData]` or `[SensitiveData]` attributes via reflection.
3. For `Modified` entities, skips properties that were not actually changed (`IsModified == false`).
4. Creates one `AuditRecord` per sensitive field and appends it to `IAuditStore`.

## Registration

```csharp
// Register the interceptor and a default NullAuditContext
builder.Services.AddInMemoryAuditStore();
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
builder.Services.AddSensitiveFlowAuditContext<MyAuditContext>();
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

## NullAuditContext

`NullAuditContext` is a thread-safe singleton (`NullAuditContext.Instance`) that returns `null` for both `ActorId` and `IpAddressToken`. Use it in tests to keep audit records free of HTTP dependencies:

```csharp
var interceptor = new SensitiveDataAuditInterceptor(store, NullAuditContext.Instance);
```
