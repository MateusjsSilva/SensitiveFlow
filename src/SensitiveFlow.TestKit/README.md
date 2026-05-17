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

## Advanced Features

### Thread-Safe Audit Store for Multi-Threaded Tests
Use ConcurrentBag-based store for concurrent test scenarios:

```csharp
var store = new ThreadSafeAuditStore();

// Multiple threads can add records safely
Parallel.For(0, 100, i =>
{
    store.AddRecord(new AuditRecord { Id = i, Entity = "Order" });
});

Assert.Equal(100, store.RecordCount);
var records = store.GetAllRecords();
```

**Components:**
- `ThreadSafeAuditStore` — Concurrent implementation using `ConcurrentBag`
- `IAuditStore` — Generic audit store interface
- No locking overhead—optimal for multi-threaded workloads

### Fixture Snapshots for State Preservation
Save and restore audit state between test runs:

```csharp
var manager = new AuditFixtureSnapshotManager();

// Save current state
manager.SaveSnapshot("before-update", currentRecords, 
    new { TestName = "OrderUpdateTest" });

// Run test modifications...

// Later, restore to previous state
var snapshot = manager.LoadSnapshot("before-update");
var restoredRecords = snapshot.Restore();
```

**Components:**
- `AuditFixtureSnapshot` — Immutable snapshot with metadata
- `AuditFixtureSnapshotManager` — Save/load/delete snapshots

### Fluent Assertion Helpers
Readable assertions for audit testing:

```csharp
auditRecords.ShouldHaveAuditCount(5);
auditRecords.ShouldHaveCreatedEntity("Customer");
auditRecords.ShouldHaveUpdatedEntity("Order");
auditRecords.ShouldHaveDeletedEntity("Invoice");
auditRecords.ShouldHaveAudited("Customer", "Email", 
    expectedOldValue: "old@example.com", 
    expectedNewValue: "new@example.com");
```

**Components:**
- `AuditAssertionExtensions` — Fluent assertion methods
- Supports entity-level and field-level assertions
- Clear, intent-revealing test code
