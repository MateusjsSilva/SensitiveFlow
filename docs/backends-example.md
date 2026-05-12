# Alternative Backend Examples

SensitiveFlow is backend-agnostic. You own the implementation of `IAuditStore`, `ITokenStore`, and `IAnonymizer`. Here are examples with different persistence layers.

## MongoDB - Audit Store

```csharp
using MongoDB.Driver;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

public sealed class MongoDbAuditStore : IBatchAuditStore
{
    private readonly IMongoCollection<AuditRecord> _collection;

    public MongoDbAuditStore(IMongoClient client, string databaseName = "SensitiveFlow")
    {
        var db = client.GetDatabase(databaseName);
        _collection = db.GetCollection<AuditRecord>("audit_records");
    }

    public async Task AppendRangeAsync(
        IReadOnlyCollection<AuditRecord> records, 
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;
        
        await _collection.InsertManyAsync(records, cancellationToken: cancellationToken);
    }

    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        await AppendRangeAsync([record], cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AuditRecord>.Filter;
        var filters = new List<FilterDefinition<AuditRecord>>();

        if (from.HasValue)
            filters.Add(builder.Gte(r => r.Timestamp, from.Value));
        if (to.HasValue)
            filters.Add(builder.Lte(r => r.Timestamp, to.Value));

        var filter = filters.Count > 0 
            ? builder.And(filters) 
            : builder.Empty;

        return await _collection
            .Find(filter)
            .SortBy(r => r.Timestamp)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AuditRecord>.Filter;
        var filters = new List<FilterDefinition<AuditRecord>>
        {
            builder.Eq(r => r.DataSubjectId, dataSubjectId)
        };

        if (from.HasValue)
            filters.Add(builder.Gte(r => r.Timestamp, from.Value));
        if (to.HasValue)
            filters.Add(builder.Lte(r => r.Timestamp, to.Value));

        return await _collection
            .Find(builder.And(filters))
            .SortBy(r => r.Timestamp)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }
}
```

### Registration

```csharp
builder.Services.AddSingleton<IMongoClient>(
    new MongoClient(builder.Configuration.GetConnectionString("MongoDB")));

builder.Services.AddScoped<IAuditStore, MongoDbAuditStore>();
builder.Services.AddSensitiveFlowEFCore();
```

---

## Redis - Token Store

```csharp
using StackExchange.Redis;
using SensitiveFlow.Core.Interfaces;
using System.Text.Json;

public sealed class RedisTokenStore : ITokenStore
{
    private readonly IDatabase _db;
    private readonly TimeSpan _ttl;

    public RedisTokenStore(IConnectionMultiplexer redis, TimeSpan? ttl = null)
    {
        _db = redis.GetDatabase();
        // Tokens live for a year by default — pick a TTL that matches your retention policy.
        _ttl = ttl ?? TimeSpan.FromDays(365);
    }

    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        
        // Hash the value to create a deterministic key
        string key = $"token:{HashValue(value)}";
        
        var existing = await _db.StringGetAsync(key);
        if (existing.HasValue)
        {
            // Extend TTL on access
            await _db.KeyExpireAsync(key, _ttl);
            return existing.ToString();
        }

        // Create new token (UUID for high entropy)
        string token = Guid.NewGuid().ToString("N");
        
        // Store bidirectionally for reversal
        await _db.StringSetAsync(key, token, _ttl);
        await _db.StringSetAsync($"reverse:{token}", value, _ttl);
        
        return token;
    }

    public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        
        var value = await _db.StringGetAsync($"reverse:{token}");
        if (!value.HasValue)
            throw new KeyNotFoundException($"Token '{token}' not found in store.");
        
        // Extend TTL on access
        await _db.KeyExpireAsync($"reverse:{token}", _ttl);
        
        return value.ToString();
    }

    private static string HashValue(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(hash);
    }
}
```

### Registration

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

builder.Services.AddScoped<ITokenStore, RedisTokenStore>();
builder.Services.AddSensitiveFlowLogging();
```

---

## Azure Table Storage - Audit Store

```csharp
using Azure.Data.Tables;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

public sealed class AzureTableAuditStore : IBatchAuditStore
{
    private readonly TableClient _client;

    public AzureTableAuditStore(string connectionString, string tableName = "AuditRecords")
    {
        _client = new TableClient(new Uri($"{connectionString}"), tableName);
    }

    public async Task AppendRangeAsync(
        IReadOnlyCollection<AuditRecord> records, 
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;

        // Azure Table Storage limits batch to 100 items
        foreach (var batch in records.Chunk(100))
        {
            var transaction = new List<TableTransactionAction>();
            
            foreach (var record in batch)
            {
                transaction.Add(new TableTransactionAction(
                    TableTransactionActionType.Add,
                    new AuditRecordEntity(record)));
            }

            await _client.SubmitTransactionAsync(transaction, cancellationToken);
        }
    }

    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        await AppendRangeAsync([record], cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(from, to);
        
        return await _client
            .QueryAsync<AuditRecordEntity>(filter, cancellationToken: cancellationToken)
            .OrderBy(e => e.Timestamp)
            .Skip(skip)
            .Take(take)
            .Select(e => e.ToAuditRecord())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = $"DataSubjectId eq '{dataSubjectId}' {BuildFilter(from, to)}";
        
        return await _client
            .QueryAsync<AuditRecordEntity>(filter, cancellationToken: cancellationToken)
            .OrderBy(e => e.Timestamp)
            .Skip(skip)
            .Take(take)
            .Select(e => e.ToAuditRecord())
            .ToListAsync(cancellationToken);
    }

    private static string BuildFilter(DateTimeOffset? from, DateTimeOffset? to)
    {
        var filters = new List<string>();
        if (from.HasValue)
            filters.Add($"Timestamp ge datetime'{from:O}'");
        if (to.HasValue)
            filters.Add($"Timestamp le datetime'{to:O}'");
        
        return filters.Count > 0 ? " and " + string.Join(" and ", filters) : "";
    }

    // Adapter for Azure Table Storage serialization
    private sealed class AuditRecordEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = "audit";
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Id { get; set; }
        public string DataSubjectId { get; set; }
        public string Entity { get; set; }
        public string Field { get; set; }
        public string Operation { get; set; }
        public string ActorId { get; set; }
        public string IpAddressToken { get; set; }
        public string Details { get; set; }

        public AuditRecordEntity() { }

        public AuditRecordEntity(AuditRecord record)
        {
            Id = record.Id;
            RowKey = record.Id;
            DataSubjectId = record.DataSubjectId;
            Entity = record.Entity;
            Field = record.Field;
            Operation = record.Operation.ToString();
            Timestamp = record.Timestamp;
            ActorId = record.ActorId;
            IpAddressToken = record.IpAddressToken;
            Details = record.Details;
        }

        public AuditRecord ToAuditRecord()
        {
            return new AuditRecord
            {
                Id = Id,
                DataSubjectId = DataSubjectId,
                Entity = Entity,
                Field = Field,
                Operation = Enum.Parse<AuditOperation>(Operation),
                Timestamp = Timestamp ?? DateTimeOffset.UtcNow,
                ActorId = ActorId,
                IpAddressToken = IpAddressToken,
                Details = Details
            };
        }
    }
}
```

### Registration

```csharp
builder.Services.AddScoped<IAuditStore>(sp =>
    new AzureTableAuditStore(builder.Configuration.GetConnectionString("AzureTableStorage")));

builder.Services.AddSensitiveFlowEFCore();
```

---

## Best Practices for Custom Backends

### 1. **Always Implement `IBatchAuditStore` if Possible**
- Reduces roundtrips from N to 1 per SaveChanges
- Critical for performance at scale

### 2. **Use Deterministic Tokens**
- Hash the value to create keys (e.g., Redis `token:{hash}`)
- Allows cache hits on repeated values
- Safe for reversible lookup

### 3. **Set TTLs / Expiration**
- Redis: `EXPIRE` on keys
- DynamoDB: `TTL` attribute
- MongoDB: TTL index
- Enables enforcement of retention policies

### 4. **Partition by DataSubjectId (Optional)**
- Improves query performance for data-subject access and erasure requests
- Enables data erasure by partition in some stores

### 5. **Audit Store Transactions**
- Batch appends should be atomic
- If main SaveChanges fails, audits should not persist
- Use store-native transactions (MongoDB sessions, SQL transactions, etc.)

### 6. **Error Handling**
- Network timeouts should not break the main flow
- Consider circuit breakers for audit store failures
- Log audit store errors separately from application logic

---

## Testing Custom Backends

```csharp
[Fact]
public async Task MongoDbAuditStore_AppendRange_InsertsRecords()
{
    var client = new MongoClient("mongodb://localhost:27017");
    var store = new MongoDbAuditStore(client, "test_db");
    
    var records = new[]
    {
        new AuditRecord { DataSubjectId = "user-1", Entity = "Order", Field = "Total" },
        new AuditRecord { DataSubjectId = "user-1", Entity = "Order", Field = "Status" }
    };
    
    await store.AppendRangeAsync(records);
    
    var result = await store.QueryByDataSubjectAsync("user-1");
    result.Should().HaveCount(2);
}
```

---

## Performance Considerations

| Store | Insert | Query by Timestamp | Query by DataSubjectId | TTL Support |
|-------|--------|-------------------|------------------------|-------------|
| SQL Server (EF Core) | ✓ Fast | ✓ Index | ✓ Index | Manual |
| MongoDB | ✓ Very Fast | ✓ Native | ✓ Native | ✓ TTL Index |
| Redis | ✓ Very Fast | ✗ Linear | ✗ Linear | ✓ Native |
| Azure Table | ✓ Fast | ✓ Range | ✓ Partition | ✓ Native |
| DynamoDB | ✓ Fast | ✓ GSI | ✓ GSI | ✓ Native |

**Recommendation**: Use MongoDB or PostgreSQL for audit trails; both are production-proven for this use case.

