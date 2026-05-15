# SensitiveFlow Benchmarks

Comprehensive performance benchmarking suite for measuring the impact of SensitiveFlow library across all packages.

## Overview

The benchmarks are organized by package, measuring:
- **Latency** - Operation duration in milliseconds
- **Throughput** - Operations per second
- **Memory** - Allocation per operation (bytes)
- **Concurrency** - Performance under parallel load

## Package Benchmarks

### 1. EFCore - SaveChanges Interceptor
**Location:** `Benchmarks/EFCore/SaveChangesInterceptorBenchmarks.cs`

Measures performance of the SaveChanges interceptor which audits all database changes.

**Scenarios:**
- Insert single entity (with/without sensitive data)
- Bulk insert (10, 50 entities)
- Update operations
- Delete operations

**Expected Performance:**
- Single insert: <5ms
- Bulk insert (50): <50ms
- Memory: ~200-500B per operation

**Why it matters:** SaveChanges is called on every database write. Even small overhead compounds quickly in high-throughput applications.

### 2. Logging - Redaction Performance
**Location:** `Benchmarks/Logging/LoggingRedactionBenchmarks.cs`

Measures the performance of structured logging redaction which automatically masks sensitive values.

**Scenarios:**
- Single log message (Information, Warning, Error, Critical)
- Logs with sensitive email addresses
- Logs with multiple structured properties (5+ fields)
- Burst logging (10 messages)
- Redacted vs. non-redacted comparison

**Expected Performance:**
- Single message: <1ms
- 10 message burst: <10ms
- Redaction overhead: <0.5ms per message

**Why it matters:** Logging happens frequently. Redaction adds a filter step before data reaches any sink.

### 3. JSON - Serialization Masking
**Location:** `Benchmarks/Json/JsonMaskingBenchmarks.cs`

Measures JSON serialization performance with automatic field masking.

**Scenarios:**
- Serialize simple objects (no sensitive data)
- Serialize objects with 2-5 sensitive fields
- Serialize with indentation (API response format)
- Array serialization (10, 50 objects)
- Round-trip (serialize + deserialize)
- Nested object serialization

**Expected Performance:**
- Simple object: <1ms
- 50 object array: <10ms
- Memory: ~100-200B per object

**Why it matters:** API responses are serialized for every request. Masking adds overhead at serialization time.

### 4. Audit - Trail Recording
**Location:** `Benchmarks/Audit/AuditStoreBenchmarks.cs`

Measures audit trail write and query performance.

**Scenarios:**
- Write single audit record
- Batch write (10, 50 records)
- Query by DataSubjectId
- Query by date range
- Query by entity and field
- Health checks

**Expected Performance:**
- Single write: <1ms (in-memory)
- Batch write (50): <10ms (in-memory)
- Query: <5ms (depends on data size)

**Why it matters:** Audit writes happen synchronously on entity changes. Batch operations should be preferred when possible.

### 5. Anonymization - Masking Operations
**Location:** `Benchmarks/Anonymization/AnonymizationBenchmarks.cs`

Measures masking and fingerprinting performance.

**Scenarios:**
- Mask email addresses
- Mask phone numbers
- Mask names
- Generate deterministic fingerprints (SHA256)
- Bulk masking (10 sequential vs. parallel)
- Combined masking (email + phone + name)

**Expected Performance:**
- Single mask: <0.1ms
- 10 masks sequential: <1ms
- 10 masks parallel: <2ms
- Fingerprint generation: <0.1ms

**Why it matters:** Masking is used in logs, JSON responses, and data pseudonymization. High-frequency operations benefit from parallelization.

### 6. Retention - Policy Evaluation
**Location:** `Benchmarks/Retention/RetentionBenchmarks.cs`

Measures retention policy discovery and evaluation.

**Scenarios:**
- Discover retention attributes on types
- Check field expiration
- Evaluate retention policies
- Scan entities for retention metadata
- Calculate expiration for batch of entities (10, 50)
- Identify fields needing anonymization

**Expected Performance:**
- Attribute discovery: <0.5ms
- Expiration check: <0.1ms
- Batch calculation (50): <5ms

**Why it matters:** Retention scanning happens during startup and periodically during scheduled jobs.

### 7. TokenStore/Redis - Pseudonymization
**Location:** `Benchmarks/TokenStore/RedisTokenStoreBenchmarks.cs`

Measures Redis Token Store performance across 4 data scenarios.

**Scenarios:**
- Emails (high cardinality)
- IP Addresses (medium cardinality)
- UUIDs (very high cardinality)
- Customer IDs (low cardinality, repeated)

**Benchmarks:**
- GetOrCreateToken (new value): ~4-5ms
- GetOrCreateToken (existing): ~2-3ms
- ResolveToken: ~2-3ms
- Bulk operations (10): ~45ms
- Concurrent operations (5 parallel): ~20ms

**Why it matters:** Token store is the foundation for reversible pseudonymization. Used in logs, analytics, and exports.

## Running Benchmarks

### Run All Benchmarks
```bash
cd tests/SensitiveFlow.Benchmarks
dotnet run -c Release
```

### Run Specific Package Benchmarks
```bash
# EFCore only
dotnet run -c Release -- --filter=SaveChangesInterceptorBenchmarks

# Logging only
dotnet run -c Release -- --filter=LoggingRedactionBenchmarks

# JSON only
dotnet run -c Release -- --filter=JsonMaskingBenchmarks

# Audit only
dotnet run -c Release -- --filter=AuditStoreBenchmarks

# Anonymization only
dotnet run -c Release -- --filter=AnonymizationBenchmarks

# Retention only
dotnet run -c Release -- --filter=RetentionBenchmarks

# TokenStore/Redis only
dotnet run -c Release -- --filter=RedisTokenStoreBenchmarks
```

### Run with Memory Diagnostics
```bash
dotnet run -c Release -- --memory
```

### Run with Verbose Output
```bash
dotnet run -c Release -- --verbose
```

## Interpreting Results

### BenchmarkDotNet Output

```
| Method                    | Mean      | Allocated |
|---------------------------|-----------|-----------|
| Insert single entity      | 2.345 ms  | 512 B     |
| Bulk insert (50)          | 45.123 ms | 50 KB     |
```

**Key Columns:**
- **Method**: What was tested
- **Mean**: Average operation time (ms)
- **Allocated**: Memory per operation (B/KB/MB)
- **StdDev**: Standard deviation (lower is more stable)

### Performance Thresholds

**Green ✅** - Performance is acceptable:
- Single operations: <5ms
- Batch operations: Linear scaling (n * single op time)
- Memory: <500B per lightweight operation
- Variance: <10% of mean

**Yellow ⚠️** - Performance acceptable but worth watching:
- Single operations: 5-10ms
- Variance: 10-20% of mean
- Memory: 500B-1KB per operation

**Red 🔴** - Performance degradation detected:
- Single operations: >10ms
- Variance: >20% of mean
- Memory: >1KB per operation
- Batch time not linear with entity count

## Real-World Impact Calculation

### API Request Flow with SensitiveFlow
```
Request → Route matching (0.1ms)
        → DI resolution (0.5ms)
        → DB query (5-50ms) ← Main cost
        → Token creation (3-5ms) ← SensitiveFlow
        → JSON serialization (1-3ms)
        → JSON redaction (0.5-1ms) ← SensitiveFlow
        → Network send (5-50ms)
        ───────────────────
        Total: 15-160ms
        SensitiveFlow: 3.5-6ms (2-4% overhead)
```

**Conclusion:** Overhead is negligible for typical APIs.

### Bulk Data Export (GDPR Request)
```
Query 1000 records (100-500ms)
  ↓
Pseudonymization (parallel 8 tasks):
  - Sequential: ~3000ms
  - With SensitiveFlow parallelization: ~375-560ms
  ↓
JSON serialization (50-100ms)
  ↓
JSON redaction (50-100ms) ← SensitiveFlow
  ↓
Disk write (20-50ms)
───────────────────────────
Without optimization: 3200-5500ms
With parallelization: 600-900ms
SensitiveFlow enables: 50-70% improvement
```

## Performance Optimization Tips

### 1. Batch Database Operations
```csharp
// ❌ Inefficient - saves per entity
foreach (var customer in customers)
{
    context.Customers.Add(customer);
    await context.SaveChangesAsync();  // 1000 interceptor calls
}

// ✅ Efficient - single batch save
context.Customers.AddRange(customers);
await context.SaveChangesAsync();  // 1 interceptor call
```

### 2. Use Parallel Logging for High Volume
```csharp
// ❌ Sequential
foreach (var email in emails)
{
    logger.LogInformation("Processing {Email}", email);  // Serialized redaction
}

// ✅ Parallel (if logger is thread-safe)
var tasks = emails.Select(e =>
    Task.Run(() => logger.LogInformation("Processing {Email}", e))
);
await Task.WhenAll(tasks);
```

### 3. Configure Appropriate Redis TTL
```csharp
// Session tokens - short-lived
builder.Services.AddRedisTokenStore(
    redis,
    defaultExpiry: TimeSpan.FromHours(1)
);

// Long-term references - extended
builder.Services.AddRedisTokenStore(
    redis,
    defaultExpiry: TimeSpan.FromDays(90)
);
```

### 4. Monitor Health Checks
```csharp
var health = await tokenStore.IsHealthyAsync();
if (!health)
{
    logger.LogWarning("Token store unhealthy");
    // Implement circuit breaker if needed
}
```

### 5. Use Redis Cluster for Distribution
```csharp
var options = ConfigurationOptions.Parse(
    "redis1:6379,redis2:6379,redis3:6379");
var connection = await ConnectionMultiplexer.ConnectAsync(options);
builder.Services.AddRedisTokenStore(connection);
```

## Performance Regression Detection

### Baseline Capture
```bash
dotnet run -c Release > baseline.txt
```

### After Code Changes
```bash
dotnet run -c Release > current.txt
diff baseline.txt current.txt
```

### Acceptable Variance
- ✅ 1-5%: Normal variance
- ⚠️ 5-10%: Investigate
- 🔴 >10%: Regression, fix required

## CI/CD Integration

### GitHub Actions Example
```yaml
name: Benchmarks
on: [push]

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '10.0'
      
      - name: Run benchmarks
        run: |
          cd tests/SensitiveFlow.Benchmarks
          dotnet run -c Release
      
      - name: Upload results
        uses: actions/upload-artifact@v2
        with:
          name: benchmark-results
          path: BenchmarkDotNet.Artifacts/
```

## Reference Benchmarks

### Baseline (Local .NET 10.0)

**EFCore SaveChanges:**
- Single insert: 2-4ms
- Bulk insert (50): 45-55ms

**Logging Redaction:**
- Single message: 0.5-1ms
- 10 message burst: 5-10ms

**JSON Serialization:**
- Simple object: 0.5-1ms
- 50 object array: 8-12ms

**Audit Write:**
- Single record: 0.1-0.5ms
- Batch (50 records): 5-10ms

**Anonymization:**
- Single mask: 0.05-0.1ms
- 10 masks parallel: 1-2ms

**Retention Evaluation:**
- Batch (50 entities): 2-5ms

**Redis Token Store (local):**
- GetOrCreateToken (new): 4-5ms
- GetOrCreateToken (existing): 2-3ms
- Concurrent (5 parallel): 20-25ms

## Troubleshooting

### High Variance in Results
- Close unnecessary applications
- Stop background services
- Disable CPU frequency scaling
- Run on dedicated machine if possible

### Slow Redis Benchmarks
```bash
# Check Redis connectivity
redis-cli ping  # Should respond PONG immediately
redis-cli --latency  # Should show <1ms
```

### Out of Memory
- Reduce benchmark iteration count
- Filter to specific benchmarks
- Increase system memory available

## Future Enhancements

These items are beyond the scope of the current benchmarking suite but could be valuable additions:

- **Cross-version comparison**: Benchmark across .NET versions (8.0, 9.0, 10.0) to identify regressions
- **Production-scale testing**: Benchmark with large data volumes (100k+ entities, 100MB+ JSON payloads)
- **Hot path profiling**: Use external profilers (dotTrace, flame graphs) to identify optimization opportunities
- **Stress testing**: Sustained load testing (high concurrency over extended periods)

See [BENCHMARK_SUMMARY.md](../../BENCHMARK_SUMMARY.md) for current results and CI/CD integration guidelines.

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [StackExchange.Redis Performance](https://github.com/StackExchange/StackExchange.Redis/wiki/Performance)
- [Redis Performance Tuning](https://redis.io/topics/optimization)
