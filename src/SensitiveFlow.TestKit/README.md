# SensitiveFlow.TestKit

Testing utilities and in-memory implementations for unit testing sensitive data protection.

## Main Components

### In-Memory Audit Store
- **`InMemoryAuditStore`** — Simple list-based audit storage
  - Thread-unsafe (suitable for single-threaded tests only)
  - Implements `IAuditStore`
  - Fast queries via LINQ to Objects
  - Perfect for unit tests

### Test Helpers
- **`AuditRecordBuilder`** — Fluent builder for test fixtures
- **`AuditQueryBuilder`** — Query construction for assertions
- Test extensions for assertions

## Usage

### Basic Test
```csharp
[Fact]
public async Task ShouldAuditEmailChange()
{
    var store = new InMemoryAuditStore();
    var interceptor = new SensitiveDataAuditInterceptor(
        store,
        NullAuditContext.Instance
    );

    // ... execute operation ...

    var records = await store.QueryAsync();
    Assert.Single(records);
    Assert.Equal("Email", records[0].Field);
}
```

### With Test Data Builder
```csharp
[Fact]
public async Task ShouldFilterByDataSubject()
{
    var store = new InMemoryAuditStore();
    
    await store.AppendAsync(new AuditRecordBuilder()
        .WithDataSubjectId("user-1")
        .WithField("Email")
        .WithOperation(AuditOperation.Update)
        .Build());

    await store.AppendAsync(new AuditRecordBuilder()
        .WithDataSubjectId("user-2")
        .WithField("Phone")
        .WithOperation(AuditOperation.Update)
        .Build());

    var records = await store.QueryAsync(
        new AuditQuery().ByDataSubjectId("user-1")
    );

    Assert.Single(records);
    Assert.Equal("user-1", records[0].DataSubjectId);
}
```

## Limitations

**NOT for integration tests** — Use real EF Core audit store instead:

```csharp
// ❌ Don't use InMemory in integration tests
var store = new InMemoryAuditStore();

// ✅ Use EF Core store with test database
builder.Services.AddDbContext<TestAuditDbContext>(options =>
    options.UseSqlite("Data Source=:memory:")
);
builder.Services.AddEfCoreAuditStore<TestAuditDbContext>();
```

## Possible Improvements

1. **Thread-safe variant** — ConcurrentBag-based store for multi-threaded tests
2. **Fixture snapshots** — Save/restore audit state between tests
3. **Assertion helpers** — `ShouldHaveAudited(entity, field)` extensions
4. **Mock implementations** — Moq/NSubstitute helpers
