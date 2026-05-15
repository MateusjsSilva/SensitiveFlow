# SensitiveFlow Troubleshooting Guide

Comprehensive troubleshooting for common issues when integrating SensitiveFlow into your .NET applications.

---

## Installation & Setup Issues

### Problem: "Missing package SensitiveFlow.Core" or "Type not found"

**Symptoms:**
- Compilation errors: `The type or namespace name 'SensitiveFlow' could not be found`
- `[PersonalData]` or `[SensitiveData]` attributes not recognized

**Solutions:**

1. **Verify package version matches target framework:**
   ```bash
   dotnet package search SensitiveFlow.Core
   dotnet add package SensitiveFlow.Core
   ```

2. **Check Directory.Packages.props** (if using Central Package Management):
   ```xml
   <PackageReference Include="SensitiveFlow.Core" Version="1.0.0-preview.4" />
   ```

3. **Restore and rebuild:**
   ```bash
   dotnet restore
   dotnet clean
   dotnet build
   ```

4. **Verify target frameworks:**
   - SensitiveFlow supports: net8.0, net9.0, net10.0
   - If targeting older frameworks, upgrade or use LTS releases

---

### Problem: "AddSensitiveFlowWeb not found" or DI registration fails

**Symptoms:**
- `IServiceCollection` has no extension method `AddSensitiveFlowWeb`
- `ServiceCollection` type resolution fails

**Solutions:**

1. **Install the composition package:**
   ```bash
   dotnet add package SensitiveFlow.AspNetCore.EFCore
   ```

2. **Add required using statements:**
   ```csharp
   using SensitiveFlow.AspNetCore.EFCore.Extensions;
   using SensitiveFlow.AspNetCore.EFCore.Profiles;
   ```

3. **Verify DbContext and DI registration order:**
   ```csharp
   // Correct order:
   builder.Services.AddSensitiveFlowWeb(...);        // Register first
   builder.Services.AddDbContext<AppDbContext>(...); // Then register DbContext
   ```

---

## Configuration Issues

### Problem: Audit records not being created or stored

**Symptoms:**
- Database queries show 0 audit records
- Changes to `[PersonalData]` fields aren't tracked
- Logs show no audit-related output

**Diagnosis:**

1. **Check if audit is enabled:**
   ```csharp
   // In AddSensitiveFlowWeb config:
   options.EnableEfCoreAudit(); // Must be called
   ```

2. **Verify entity has DataSubjectId or UserId:**
   ```csharp
   public class Order
   {
       public Guid Id { get; set; }
       public string DataSubjectId { get; set; } // ← Required
       
       [PersonalData]
       public string CustomerEmail { get; set; }
   }
   ```

3. **Check that at least one field is annotated:**
   ```csharp
   [PersonalData]  // ← Must mark sensitive fields
   public string Email { get; set; }
   ```

4. **Verify audit database is accessible:**
   ```bash
   # Test connection string
   dotnet ef dbcontext info --context SensitiveFlowAuditDbContext
   ```

5. **Check audit interceptor is registered:**
   ```csharp
   options.UseEfCoreStores(
       audit => audit.UseSqlServer(auditConnStr),  // ← Configured
       tokens => tokens.UseSqlServer(tokenConnStr)
   );
   ```

**Solution:**

If still not working, enable debug logging:

```csharp
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

Then check logs for:
- `[SensitiveFlow.Audit]` entries
- `[SensitiveFlow.EFCore]` interceptor output
- Connection errors or timeouts

---

### Problem: "Token store not initialized" or ITokenStore is null

**Symptoms:**
- NullReferenceException when injecting `ITokenStore`
- `InvalidOperationException: Cannot resolve service`

**Solutions:**

1. **Register token store backend:**
   ```csharp
   options.UseEfCoreStores(
       audit => audit.UseSqlServer(...),
       tokens => tokens.UseSqlServer(...)  // ← Token store
   );
   ```

2. **For Redis token store:**
   ```csharp
   builder.Services.AddSingleton<IConnectionMultiplexer>(
       ConnectionMultiplexer.Connect("localhost:6379"));
   builder.Services.AddRedisTokenStore(redis);
   ```

3. **Verify `AddSensitiveFlowWeb` is called before `BuildServiceProvider`:**
   ```csharp
   // Wrong:
   var provider = builder.Services.BuildServiceProvider();
   builder.Services.AddSensitiveFlowWeb(...);  // Too late
   
   // Correct:
   builder.Services.AddSensitiveFlowWeb(...);
   var provider = builder.Services.BuildServiceProvider();
   ```

---

## Runtime Issues

### Problem: JSON redaction not working

**Symptoms:**
- Sensitive fields appear in JSON API responses
- `[PersonalData]` fields are not masked
- Client receives unredacted data

**Solutions:**

1. **Enable JSON redaction:**
   ```csharp
   options.EnableJsonRedaction();  // Must be called
   ```

2. **Annotate DTO properties:**
   ```csharp
   // Entity
   public class Customer
   {
       [PersonalData]
       public string Email { get; set; }
   }
   
   // DTO
   public class CustomerDto
   {
       [PersonalData]  // ← Also annotate in DTO
       public string Email { get; set; }
   }
   ```

3. **Verify serializer configuration:**
   ```csharp
   // If using custom JsonSerializerOptions:
   var options = new JsonSerializerOptions();
   options.AddSensitiveFlowRedaction();  // Add the converter
   ```

4. **Check response type:**
   - Works for: `application/json` responses
   - Works with: System.Text.Json serializer
   - Does NOT work with: Newtonsoft.Json (register custom converter)

5. **Test with curl:**
   ```bash
   curl https://localhost:5001/api/customers/123
   # Check response for [REDACTED] markers
   ```

---

### Problem: Log redaction not working or too much data logged

**Symptoms:**
- Sensitive values appear in logs
- `[PersonalData]` fields logged unmasked
- Logs are too verbose with audit details

**Solutions:**

1. **Enable logging redaction:**
   ```csharp
   options.EnableLoggingRedaction();
   ```

2. **Configure log level for sensitive components:**
   ```csharp
   builder.Logging.AddFilter("SensitiveFlow.Redaction", LogLevel.Warning);
   builder.Logging.AddFilter("SensitiveFlow.Audit.EFCore", LogLevel.Information);
   ```

3. **Exclude sensitive loggers:**
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "SensitiveFlow.Audit.EFCore.Interceptors": "Warning",
         "SensitiveFlow.TokenStore": "Warning"
       }
     }
   }
   ```

---

### Problem: Token store timeouts or "Redis connection refused"

**Symptoms:**
- Timeout exceptions from Redis
- `StackExchange.Redis.RedisConnectionException`
- Pseudonymization requests hang or fail

**Solutions:**

1. **Check Redis is running:**
   ```bash
   # Local Redis
   redis-cli ping
   # Should respond: PONG
   
   # Docker Redis
   docker ps | grep redis
   ```

2. **Verify connection string:**
   ```csharp
   var conn = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
   await conn.Server.PingAsync();
   ```

3. **Check firewall/network:**
   ```bash
   # Test connectivity
   telnet localhost 6379
   ```

4. **Increase timeout for slow networks:**
   ```csharp
   var options = ConfigurationOptions.Parse("localhost:6379");
   options.ConnectTimeout = 5000;
   options.SyncTimeout = 5000;
   var conn = await ConnectionMultiplexer.ConnectAsync(options);
   
   builder.Services.AddSingleton<IConnectionMultiplexer>(conn);
   builder.Services.AddRedisTokenStore(conn);
   ```

5. **For Sentinel/Cluster:**
   ```csharp
   var options = ConfigurationOptions.Parse(
       "sentinel1:26379,sentinel2:26379,serviceName=mymaster");
   var conn = await ConnectionMultiplexer.ConnectAsync(options);
   ```

---

## Data Integrity Issues

### Problem: Audit records show NULL or empty values

**Symptoms:**
- `AuditRecord.Value` is null
- `AuditRecord.Details` is empty
- Can't reconstruct audit trail

**Solutions:**

1. **Verify values are actually being set:**
   ```csharp
   // Check if interceptor sees the change
   var entity = new Order { CustomerId = "123" };
   context.Orders.Add(entity);
   await context.SaveChangesAsync();
   
   // Query audit store
   var audit = await auditStore.QueryByDataSubjectAsync("123");
   ```

2. **Check DataSubjectId is stable:**
   ```csharp
   // Bad: ID changes
   var order = new Order { DataSubjectId = Guid.NewGuid().ToString() };
   
   // Good: Consistent ID
   var order = new Order { DataSubjectId = userId };
   ```

3. **Verify field is marked as sensitive:**
   ```csharp
   [PersonalData]
   public string Email { get; set; }  // Marked
   
   public string Phone { get; set; }   // Not marked - won't be audited
   ```

---

### Problem: "Duplicate key" or "constraint violation" in audit store

**Symptoms:**
- `SqlException: Violation of PRIMARY KEY`
- Audit inserts fail
- `RecordId` conflicts

**Solutions:**

1. **Verify RecordId generation is unique:**
   - SensitiveFlow uses `Guid.NewGuid()` by default
   - If overridden, ensure globally unique

2. **Check for concurrent writes:**
   ```csharp
   // Enable retry policy
   options.EnableAuditStoreRetry(retryCount: 3);
   ```

3. **Verify unique constraint:**
   ```sql
   -- Check constraint exists
   SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
   WHERE TABLE_NAME='AuditRecords' AND CONSTRAINT_TYPE='UNIQUE'
   ```

---

## Performance Issues

### Problem: Slow pseudonymization or token lookups

**Symptoms:**
- API requests hang (> 1s)
- High CPU during token operations
- Redis commands show slow response

**Solutions:**

1. **Profile token store calls:**
   ```csharp
   var sw = System.Diagnostics.Stopwatch.StartNew();
   var token = await tokenStore.GetOrCreateTokenAsync(value);
   sw.Stop();
   logger.LogInformation("Token store took {Elapsed}ms", sw.ElapsedMilliseconds);
   ```

2. **Check Redis performance:**
   ```bash
   redis-cli --stat  # Real-time stats
   redis-cli slowlog get 10  # Slow queries
   ```

3. **For high-throughput scenarios:**
   - Use connection pooling: `StackExchange.Redis` does this automatically
   - Enable Redis pipelining for batch operations
   - Consider Redis Cluster for horizontal scaling

4. **Monitor token store health:**
   ```csharp
   var tokenStore = provider.GetRequiredService<ITokenStore>();
   var isHealthy = await tokenStore.IsHealthyAsync();
   ```

---

## Testing Issues

### Problem: Tests fail with "ITokenStore not registered"

**Symptoms:**
- Unit test throws `InvalidOperationException: Cannot resolve service`
- Mock setup doesn't work

**Solutions:**

1. **Mock the token store:**
   ```csharp
   var mockTokenStore = new Mock<ITokenStore>();
   mockTokenStore
       .Setup(ts => ts.GetOrCreateTokenAsync(It.IsAny<string>(), default))
       .ReturnsAsync((string v, CancellationToken _) => $"tok_{v.GetHashCode()}");
   
   var services = new ServiceCollection();
   services.AddScoped(_ => mockTokenStore.Object);
   ```

2. **Use TestContainers for integration tests:**
   ```csharp
   var redis = new RedisBuilder().Build();
   await redis.StartAsync();
   
   var conn = await ConnectionMultiplexer.ConnectAsync(redis.GetConnectionString());
   var tokenStore = new RedisTokenStore(conn);
   ```

3. **For in-memory testing:**
   ```csharp
   // Use in-memory token store
   services.AddSingleton<ITokenStore>(
       new InMemoryTokenStore());  // Implement for testing
   ```

---

## Deployment Issues

### Problem: Pipeline integration fails

**Symptoms:**
- CI/CD build fails with "missing dependencies"
- Docker build fails
- Kubernetes pod doesn't start

**Solutions:**

1. **Ensure all packages in lock file:**
   ```bash
   dotnet restore --locked-mode
   ```

2. **In Docker:**
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
   COPY Directory.Packages.props .
   RUN dotnet restore
   ```

3. **Kubernetes ConfigMap for Redis:**
   ```yaml
   apiVersion: v1
   kind: ConfigMap
   metadata:
     name: sensitiveflow-config
   data:
     ConnectionStrings__Redis: "redis-service:6379"
   ```

4. **Health check endpoint:**
   ```csharp
   app.MapHealthChecks("/health", new HealthCheckOptions
   {
       ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
   });
   ```

   Then in deployment:
   ```yaml
   livenessProbe:
     httpGet:
       path: /health
       port: 8080
   ```

---

## FAQ

### Q: Can I use SensitiveFlow with NoSQL databases?

**A:** Yes. SensitiveFlow is backend-agnostic:
- `IAuditStore`: Implement for MongoDB, DynamoDB, Cosmos DB, etc.
- `ITokenStore`: Implement for any persistent store
- See [backends-example.md](backends-example.md) for examples

---

### Q: What's the performance impact on SaveChanges?

**A:** Minimal (<5% overhead for typical CRUD):
- Audit interception happens on `SaveChanges`
- Async write to separate audit store
- No blocking the main transaction

---

### Q: Can I disable redaction for development?

**A:** Yes:
```csharp
if (app.Environment.IsDevelopment())
{
    options.DisableJsonRedaction();
    options.DisableLoggingRedaction();
}
```

---

### Q: How do I export data for GDPR requests?

**A:** Use `IDataSubjectExporter`:
```csharp
var exporter = provider.GetRequiredService<IDataSubjectExporter>();
var userData = await exporter.ExportAsync(dataSubjectId);
```

See [anonymization.md](anonymization.md) for details.

---

## Getting Help

- **Documentation**: [SensitiveFlow Docs](.)
- **Issues**: GitHub Issues (with reproducible example)
- **Examples**: `samples/` directory for working code
- **Tests**: `tests/` directory show real usage patterns
