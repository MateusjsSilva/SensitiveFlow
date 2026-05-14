# SensitiveFlow.EFCore

Entity Framework Core integration for automatic auditing of sensitive data during database operations.

## Main Components

### Interceptors
- **`SensitiveDataAuditInterceptor`** — SaveChangesInterceptor capturing mutations of sensitive fields
  - Hooks into `SavingChangesAsync` to collect records before commit
  - Hooks into `SavedChangesAsync` to persist records after success
  - Supports Add, Modify, Delete operations
  - Requires `IAuditStore` and `IAuditContext` registered

- **`SensitiveBulkOperationsGuardInterceptor`** — IQueryExpressionInterceptor blocking unaudited bulk operations
  - Detects direct `ExecuteUpdateAsync` / `ExecuteDeleteAsync` on annotated entities
  - Can be disabled via `SensitiveBulkOperationsOptions.RequireExplicitAuditing = false`

### Extensions
- **`ExecuteUpdateAuditedAsync<T>`** — Audited version of `ExecuteUpdateAsync`
  - Prefetches `DataSubjectId` values via SELECT
  - Emits one `AuditRecord` per (subject, field)
  - Uses tag `__SensitiveFlow:Audited__` to mark audited queries

- **`ExecuteDeleteAuditedAsync<T>`** — Audited version of `ExecuteDeleteAsync`
  - Same prefetch and record emission approach

### Configuration
- **`AddSensitiveFlowEFCore()`** — Registers interceptor with `NullAuditContext` by default
- **`AddEfCoreAuditStore<TDbContext>()`** — Registers audit storage for specific DbContext
- **`AddInterceptors()`** on DbContext — Wires the interceptors

## How It Works

### SaveChanges Flow
1. `SavingChanges` captures entities in state `Added|Modified|Deleted`
2. Filters to only entities with sensitive properties
3. For `Modified`, skips properties not actually changed (`IsModified == false`)
4. Creates `AuditRecord` per (subject, field, operation)
5. Stores in `PendingAuditRecords` (ConditionalWeakTable)
6. `SavedChanges` (after success) persists records via `IAuditStore`

### Bulk Operations Flow
1. Query expression interceptor detects `ExecuteUpdate/DeleteAsync`
2. If entity is annotated and missing `__SensitiveFlow:Audited__` tag, throws error
3. Helpers `ExecuteUpdateAuditedAsync` execute:
   - Prefetch: SELECT of `DataSubjectId` for records to be modified
   - Execute: Actual modification
   - Emit: Insert `AuditRecord` per (subject, field)
4. Cost guard: `SensitiveBulkOperationsOptions.MaxAuditedRows` (default 10,000)

## Audit Context

### Provided Implementations
- **`NullAuditContext`** — Returns null for `ActorId` and `IpAddressToken`
- **`HttpAuditContext`** (via AspNetCore) — Extracts from HttpContext (user, IP)

### Custom Implementation
```csharp
public sealed class MyAuditContext : IAuditContext
{
    private readonly ICurrentUserService _user;

    public MyAuditContext(ICurrentUserService user) => _user = user;
    public string? ActorId => _user.UserId;
    public string? IpAddressToken => null;
}

builder.Services.AddScoped<IAuditContext, MyAuditContext>();
```

## Validations

### DataSubjectId
- **Required**: Public property `DataSubjectId` (or `UserId` as legacy alias)
- **Validation**: Non-empty string at SaveChanges time
- **Validation failure**: Throws `InvalidOperationException` in `CaptureAuditRecords`

### Bulk Operations
- **MaxAuditedRows**: Prefetch reads +1 row; if exceeds limit, throws error
- **EmptySubjectId**: Validates during prefetch; entities with empty subject throw error

## Redaction Behavior

### Per Action
- `OutputRedactionAction.None` — Does not include `Details`
- `OutputRedactionAction.Redact` — Details: "[REDACTED]"
- `OutputRedactionAction.Mask` — Details: Masked email, partial name
- `OutputRedactionAction.Pseudonymize` — Details: Token via `IPseudonymizer`
- `OutputRedactionAction.Omit` — **Never affects field in audit**, only output

## Concurrency Considerations

### Race Condition in Bulk Operations
```
Prefetch SELECT → Execute UPDATE → Insert Audit
     ↓
Between prefetch and update:
- New rows inserted: Modified but NOT audited
- Rows deleted: Audited for subjects that no longer exist
```

**Solution**: Use explicit transaction for critical operations

```csharp
using var txn = await db.Database.BeginTransactionAsync();
try
{
    await db.Customers.ExecuteUpdateAuditedAsync(...);
    await txn.CommitAsync();
}
catch { await txn.RollbackAsync(); throw; }
```

**Note**: Typical in background jobs where concurrency is controlled.

## Limitations

1. **Raw SQL not intercepted** — `Database.ExecuteSqlRaw/Interpolated` doesn't generate audit records
2. **SaveChanges vs SaveChangesAsync** — Sync path has deadlock risk in ASP.NET Core (see comments in SensitiveDataAuditInterceptor)
3. **ILogger<T> not detected** — Only `ILogger` and `LoggerExtensions` (SF0001 analyzer limitation)

## Possible Improvements

1. **Raw SQL interceptor** — Very complex; manual wrapping recommended
2. **EF Core Migrations support** — Currently no support; auditing is runtime-only
3. **Record compression** — For high volumes, aggregate multiple changes per subject/session
4. **Replay testing with fixtures** — Facilitate testing with audit snapshots
