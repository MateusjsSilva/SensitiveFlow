# Audit Outbox Example

This example demonstrates how to set up the durable audit outbox with EF Core in a production-ready application.

## Installation

```bash
dotnet add package SensitiveFlow.Audit.EFCore.Outbox
```

## Setup

### 1. Enable Audit Store and Durable Outbox

```csharp
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.EFCore.Outbox.Extensions;

var builder = WebApplication.CreateBuilder(args);

var auditConnection = builder.Configuration.GetConnectionString("Audit")
    ?? "Data Source=audit.db";

// Register durable audit store
builder.Services.AddEfCoreAuditStore(options => 
    options.UseSqlite(auditConnection)); // or UseSqlServer, UseNpgsql

// Register durable outbox with background dispatcher
builder.Services.AddEfCoreAuditOutbox(options =>
{
    // Polls every 2 seconds for pending entries
    options.PollInterval = TimeSpan.FromSeconds(2);
    
    // Process 100 entries per batch
    options.BatchSize = 100;
    
    // Retry failed deliveries up to 5 times
    options.MaxAttempts = 5;
    
    // Use exponential backoff: 1s, 2s, 4s, 8s, 16s
    options.BackoffStrategy = BackoffStrategy.Exponential;
});
```

### 2. Implement Audit Outbox Publishers

Publishers are responsible for delivering audit records to your downstream systems (SIEM, data lakes, Kafka, webhooks, etc.).

#### Example: HTTP Publisher to SIEM

```csharp
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using System.Text.Json;

namespace YourApp.Audit;

public sealed class SiemHttpPublisher : IAuditOutboxPublisher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SiemHttpPublisher> _logger;

    public SiemHttpPublisher(HttpClient httpClient, ILogger<SiemHttpPublisher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task PublishAsync(AuditOutboxEntry entry, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(entry.Record);
        
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/audit/receive")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            Headers = { { "X-Audit-Attempt", entry.Attempts.ToString() } },
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"SIEM rejected audit record: {response.StatusCode}");
        }

        _logger.LogInformation("Delivered audit record {RecordId} to SIEM", 
            entry.Record.Id);
    }
}
```

#### Register the Publisher

```csharp
// Configure HttpClient for SIEM
builder.Services
    .AddHttpClient<SiemHttpPublisher>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["SiemEndpoint"]
            ?? "https://siem.example.com");
        client.DefaultRequestHeaders.Add("Authorization",
            $"Bearer {builder.Configuration["SiemToken"]}");
    });

// Register as IAuditOutboxPublisher
builder.Services.AddScoped<IAuditOutboxPublisher>(
    sp => sp.GetRequiredService<SiemHttpPublisher>());
```

#### Example: Kafka Publisher

```csharp
using Confluent.Kafka;

public sealed class KafkaAuditPublisher : IAuditOutboxPublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaAuditPublisher(IProducer<string, string> producer, string topic)
    {
        _producer = producer;
        _topic = topic;
    }

    public async Task PublishAsync(AuditOutboxEntry entry, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(entry.Record);
        
        var message = new Message<string, string>
        {
            Key = entry.Record.Id.ToString(),
            Value = json,
        };

        await _producer.ProduceAsync(_topic, message, cancellationToken);
    }
}
```

### 3. Create/Migrate Audit Database

The outbox table is automatically created when you run migrations:

```csharp
using var app = builder.Build();

// Apply migrations or create database
using (var scope = app.Services.CreateScope())
{
    var auditDb = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<AuditDbContext>>()
        .CreateDbContext();
    
    // For development only:
    await auditDb.Database.EnsureCreatedAsync();
    
    // For production, use EF Core migrations:
    // await auditDb.Database.MigrateAsync();
}

app.Run();
```

## Operations

### Querying Outbox State

```csharp
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<AuditDbContext>>();
    
    await using var ctx = await factory.CreateDbContextAsync();
    
    // Pending entries (not yet processed)
    var pending = await ctx.Set<AuditOutboxEntryEntity>()
        .Where(e => !e.IsProcessed && !e.IsDeadLettered)
        .ToListAsync();
    
    Console.WriteLine($"Pending: {pending.Count}");
    
    // Dead-lettered entries (failed after max retries)
    var deadLetters = await ctx.Set<AuditOutboxEntryEntity>()
        .Where(e => e.IsDeadLettered)
        .ToListAsync();
    
    foreach (var entry in deadLetters)
    {
        Console.WriteLine(
            $"Dead Letter: {entry.AuditRecordId} - {entry.DeadLetterReason}");
    }
    
    // High-retry entries (approaching max attempts)
    var risky = await ctx.Set<AuditOutboxEntryEntity>()
        .Where(e => !e.IsProcessed && e.Attempts >= 3)
        .ToListAsync();
    
    Console.WriteLine($"High-Retry: {risky.Count}");
}
```

### Health Checks

```csharp
builder.Services.AddSensitiveFlowHealthChecks()
    .AddAuditStoreCheck();

app.MapHealthChecks("/health");
```

## Guarantees

- **Transactional Enqueue**: Audit record and outbox entry are persisted in one `SaveChanges` transaction
- **At-Least-Once Delivery**: Failed deliveries are retried with configurable backoff
- **Dead-Lettering**: Entries exceeding `MaxAttempts` are marked `IsDeadLettered` for manual inspection
- **Concurrent Safe**: Multiple app instances can run simultaneously; polling is coordinated via database state

## Monitoring

Monitor the `AuditOutboxEntry` table for:

1. **Pending entries** — should be near zero in steady state
2. **Failed/dead-lettered entries** — indicates delivery issues
3. **Retry counts** — rising retry counts suggest unstable downstream systems
4. **Table size** — verify disk space is available; consider archiving old processed entries

Example SQL Server query for monitoring:

```sql
SELECT 
    COUNT(*) FILTER (WHERE IsProcessed = 0 AND IsDeadLettered = 0) as Pending,
    COUNT(*) FILTER (WHERE IsDeadLettered = 1) as DeadLettered,
    MAX(Attempts) as MaxAttempts,
    AVG(Attempts) as AvgAttempts
FROM [sensitiveflow].[AuditOutboxEntries];
```
