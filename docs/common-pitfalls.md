# Common Pitfalls & Best Practices

Avoid these 12 common mistakes when using SensitiveFlow.

---

## 1. Forgetting to Annotate DTO Properties

### ❌ WRONG: Annotation only on entity

```csharp
// Entity
public class User
{
    [PersonalData]
    public string Email { get; set; }
}

// DTO - No annotation!
public class UserDto
{
    public string Email { get; set; }  // ← Will NOT be redacted
}
```

### ✅ CORRECT: Annotate both entity and DTO

```csharp
// Entity
public class User
{
    [PersonalData]
    public string Email { get; set; }
}

// DTO - Also annotated
public class UserDto
{
    [PersonalData]  // ← Redaction applies here too
    public string Email { get; set; }
}
```

**Why:** JSON redaction works on what's serialized, not the source. Annotate all sensitive fields in DTOs.

---

## 2. Using Non-Stable DataSubjectId

### ❌ WRONG: ID changes between requests

```csharp
public class Order
{
    public string DataSubjectId { get; set; } = Guid.NewGuid().ToString();  // ← Changes every time!
    public string OrderNumber { get; set; }
}
```

**Problem:** Same order has different audit trail IDs, making correlations impossible.

### ✅ CORRECT: Use consistent, externally-provided ID

```csharp
public class Order
{
    public string DataSubjectId { get; set; }  // Set from current user context
    public string OrderNumber { get; set; }
}

// In service/handler:
var order = new Order
{
    DataSubjectId = currentUserId,  // ← Stable across requests
    OrderNumber = "ORD-123"
};
```

**Why:** Audit trails rely on DataSubjectId for querying related records. It must be stable.

---

## 3. Mixing Audit Store and Token Store Connections

### ❌ WRONG: Using same connection for both

```csharp
var connString = "Server=localhost;Database=SingleDb";  // One database!

options.UseEfCoreStores(
    audit => audit.UseSqlServer(connString),
    tokens => tokens.UseSqlServer(connString)  // Same table/schema risk
);
```

**Problem:** Audit and token data mixed in one store. Hard to separate for compliance. No isolation.

### ✅ CORRECT: Separate databases or schemas

```csharp
var auditConnString = "Server=localhost;Database=AuditDb";
var tokenConnString = "Server=localhost;Database=TokenDb";

options.UseEfCoreStores(
    audit => audit.UseSqlServer(auditConnString),
    tokens => tokens.UseSqlServer(tokenConnString)
);
```

**Alternative with schema isolation:**

```csharp
options.UseEfCoreStores(
    audit => audit.UseSqlServer(
        connString,
        schema: "audit"),  // ← Different schema
    tokens => tokens.UseSqlServer(
        connString,
        schema: "tokens")   // ← Isolated
);
```

**Why:** Compliance/auditing requires separation. GDPR erasure should not affect audit logs.

---

## 4. Ignoring TTL/Retention Policies

### ❌ WRONG: Tokens live forever

```csharp
builder.Services.AddRedisTokenStore(redis);  // No TTL set!
// Tokens accumulate indefinitely
```

**Problem:** Memory bloat, old tokens never expire, compliance violations.

### ✅ CORRECT: Set appropriate TTL

```csharp
// For short-lived session tokens:
builder.Services.AddRedisTokenStore(
    redis,
    defaultExpiry: TimeSpan.FromHours(1)
);

// For long-term data references:
builder.Services.AddRedisTokenStore(
    redis,
    defaultExpiry: TimeSpan.FromDays(90)
);
```

**Why:** Retention policies are a compliance requirement. TTL prevents accidental data leaks.

---

## 5. Not Validating at Startup

### ❌ WRONG: Relying on runtime discovery

```csharp
builder.Services.AddSensitiveFlowWeb(options =>
{
    // No validation enabled
    // options.EnableValidation();  ← Commented out
});
```

**Problem:** Misconfigured annotations only discovered in production.

### ✅ CORRECT: Enable startup validation

```csharp
builder.Services.AddSensitiveFlowWeb(options =>
{
    options.EnableValidation();  // ← Check configuration at startup
    
    options.UseEfCoreStores(
        audit => audit.UseSqlServer(...),
        tokens => tokens.UseSqlServer(...)
    );
    
    options.EnableEfCoreAudit();
    options.EnableLoggingRedaction();
    options.EnableJsonRedaction();
});
```

**What it checks:**
- All `[PersonalData]` fields have corresponding `[Redaction]`
- Audit store is reachable
- Token store is registered
- DbContext has audit interceptor

**Why:** Fail fast at startup, not during request handling.

---

## 6. Not Marking Non-PII Sensitive Data

### ❌ WRONG: Only marking PII as sensitive

```csharp
public class Payment
{
    [PersonalData]
    public string CardholderName { get; set; }  // ← Marked
    
    public string CardNumber { get; set; }  // ← NOT marked! But it's super sensitive!
    
    public decimal Amount { get; set; }  // Unmarked
}
```

### ✅ CORRECT: Mark all sensitive data

```csharp
public class Payment
{
    [PersonalData(Category = DataCategory.Identification)]
    public string CardholderName { get; set; }
    
    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    public string CardNumber { get; set; }
    
    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    public decimal Amount { get; set; }
}
```

**Why:** Compliance covers more than PII. Financial, medical, and proprietary data also require protection.

---

## 7. Enabling Features Without Understanding Impact

### ❌ WRONG: Enabling everything

```csharp
options.EnableEfCoreAudit();           // Writes to DB
options.EnableLoggingRedaction();      // Filter processor
options.EnableJsonRedaction();         // Serializer overhead
options.EnableRetention();             // Background job
options.EnableDataSubjectExport();     // Full table scan
options.EnableDataSubjectErasure();    // Deletes data
options.EnableOutbox();                // Event sourcing
options.EnableDiagnostics();           // Memory overhead
// This is NOT a "set and forget" library!
```

**Problem:** Unexpected performance degradation, memory usage, side effects.

### ✅ CORRECT: Enable features based on requirements

```csharp
options.EnableEfCoreAudit();        // ← Always for compliance
options.EnableLoggingRedaction();   // ← If logging sensitive data
options.EnableJsonRedaction();      // ← If returning sensitive JSON

if (app.Environment.IsProduction())
{
    options.EnableRetention();      // ← For GDPR compliance
}

if (featureFlags.ExportDataEnabled)
{
    options.EnableDataSubjectExport();  // ← For GDPR portability
}
```

**Why:** Each feature has CPU/memory/database impact. Enable selectively.

---

## 8. Assuming Redaction Covers Logging

### ❌ WRONG: Thinking JSON redaction covers logs

```csharp
logger.LogInformation("User email: {Email}", user.Email);  
// Logs JSON redaction ONLY, not structured logging!
// This email is logged in plain text
```

### ✅ CORRECT: Enable logging redaction explicitly

```csharp
options.EnableLoggingRedaction();  // ← Enable this

// Now logs are redacted:
logger.LogInformation("User email: {Email}", user.Email);  // ← [REDACTED]
```

**Difference:**
- JSON redaction: `System.Text.Json` serialization output
- Logging redaction: Structured logging (ILogger) output

**Why:** Logs and JSON are separate pipelines. Both need protection.

---

## 9. Hardcoding Connection Strings

### ❌ WRONG: Hardcoded in code

```csharp
builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseEfCoreStores(
        audit => audit.UseSqlServer("Server=localhost;Database=Audit"),
        tokens => tokens.UseSqlServer("Server=localhost;Database=Tokens")
    );
});
```

**Problem:** Credentials in source code, no environment flexibility.

### ✅ CORRECT: Use configuration

```csharp
builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseEfCoreStores(
        audit => audit.UseSqlServer(
            builder.Configuration.GetConnectionString("Audit")),
        tokens => tokens.UseSqlServer(
            builder.Configuration.GetConnectionString("Tokens"))
    );
});
```

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "Audit": "Server=localhost;Database=Audit",
    "Tokens": "Server=localhost;Database=Tokens"
  }
}
```

**appsettings.Production.json:**
```json
{
  "ConnectionStrings": {
    "Audit": "Server=prod-audit-db.internal;Database=Audit;User=app;",
    "Tokens": "Server=prod-token-db.internal;Database=Tokens;User=app;"
  }
}
```

**Why:** Environment parity, secret management, Kubernetes ConfigMaps support.

---

## 10. Not Handling Null DataSubjectId

### ❌ WRONG: Allowing null

```csharp
public class Document
{
    public string? DataSubjectId { get; set; }  // ← Nullable!
    
    [PersonalData]
    public string Content { get; set; }
}

// In service:
var doc = new Document { Content = "..." };  // DataSubjectId is null!
context.Documents.Add(doc);
await context.SaveChangesAsync();
// What does audit trail show for this?
```

### ✅ CORRECT: Make it required

```csharp
public class Document
{
    public string DataSubjectId { get; set; } = string.Empty;  // ← Required
    
    [PersonalData]
    public string Content { get; set; }
}

// Validation catches this:
if (string.IsNullOrEmpty(doc.DataSubjectId))
    throw new InvalidOperationException("DataSubjectId required");
```

**Why:** Null DataSubjectId breaks audit correlation and GDPR requests.

---

## 11. Mixing Token Store Implementations

### ❌ WRONG: Changing stores in production

```csharp
// Started with EF Core
builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseEfCoreStores(
        audit => ...,
        tokens => tokens.UseSqlServer(...)  // ← Old tokens here
    );
});

// Later, switched to Redis without migration
builder.Services.AddRedisTokenStore(redis);  // ← New tokens here
// Old tokens lost, can't reverse mappings!
```

### ✅ CORRECT: Plan migrations carefully

```csharp
// Approach 1: Dual-write during transition
public class MigratingTokenStore : ITokenStore
{
    private readonly ITokenStore _old;
    private readonly ITokenStore _new;
    
    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken ct)
    {
        var token = await _new.GetOrCreateTokenAsync(value, ct);
        _ = _old.GetOrCreateTokenAsync(value, ct);  // Fire-and-forget backup write
        return token;
    }
    
    public async Task<string> ResolveTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            return await _new.ResolveTokenAsync(token, ct);
        }
        catch (KeyNotFoundException)
        {
            return await _old.ResolveTokenAsync(token, ct);  // Fallback
        }
    }
}
```

**Why:** Token stores are source-of-truth for reversals. Losing them means data is permanently unrecoverable.

---

## 12. Not Testing Redaction in Integration Tests

### ❌ WRONG: Only unit testing redaction

```csharp
[Fact]
public void RedactionAttribute_Masks_Values()
{
    // Unit test passes, but...
}

// Integration test NOT written
// API actually returns unredacted data!
```

### ✅ CORRECT: Test end-to-end

```csharp
[Fact]
public async Task GetUser_ReturnsRedactedEmail()
{
    // Arrange
    var user = new User { Id = 1, Email = "test@example.com" };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    
    // Act
    var response = await client.GetAsync("/api/users/1");
    var json = await response.Content.ReadAsStringAsync();
    
    // Assert
    json.Should().Contain("[REDACTED]");  // ← Email is masked
    json.Should().NotContain("test@example.com");  // ← NOT plain text
}
```

**Test scenarios:**
- JSON responses are redacted
- Logs don't contain sensitive data
- Audit records store hashed values, not plain text
- Different redaction profiles work as intended

**Why:** Redaction failures are silent. You need tests to catch them.

---

## Summary Checklist

Before deploying to production:

- [ ] All `[PersonalData]` / `[SensitiveData]` annotated on entities AND DTOs
- [ ] DataSubjectId is stable and consistent
- [ ] Audit and token stores are separate (at least separate schemas)
- [ ] TTL/retention policies configured appropriately
- [ ] Startup validation enabled
- [ ] All sensitive data (not just PII) is marked
- [ ] Features enabled selectively, not wholesale
- [ ] Both JSON and logging redaction tested
- [ ] Connection strings from configuration, not hardcoded
- [ ] DataSubjectId is never null
- [ ] Token store migration strategy planned
- [ ] End-to-end integration tests verify redaction works

---

**Next:** See [troubleshooting.md](troubleshooting.md) if you hit issues.
