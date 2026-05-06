// ---------------------------------------------
// SensitiveFlow - Console Sample
//
// Shows a real persistence setup:
//   - SQLite via EF Core (durable AuditStore + TokenStore)
//   - Serilog for structured log redaction
//   - OpenTelemetry for activity traces
//   - [PersonalData] / [SensitiveData] attributes + retention evaluation
// ---------------------------------------------

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using SensitiveFlow.Anonymization.Anonymizers;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Masking;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.Retention.Services;

// ── Serilog ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/sensitiveflow-console-.log",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

// ── OpenTelemetry ───────────────────────────────────────────────────────────
var activitySource = new ActivitySource("SensitiveFlow.Console.Sample");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("SensitiveFlow.Console.Sample"))
    .AddSource(activitySource.Name)
    .AddConsoleExporter()
    .Build();

// ── EF Core: SQLite with durable AuditStore and TokenStore ─────────────────
var options = new DbContextOptionsBuilder<SampleDbContext>()
    .UseSqlite("Data Source=sensitiveflow-console.db")
    .Options;

using var db = new SampleDbContext(options);
await db.Database.EnsureCreatedAsync();

IAuditStore auditStore = db.AuditStore;
ITokenStore tokenStore = db.TokenStore;
var pseudonymizer = new TokenPseudonymizer(tokenStore);

// The EF Core interceptor wires into SaveChanges and emits AuditRecords automatically.
// In a DI setup, inject SensitiveDataAuditInterceptor via AddSensitiveFlowEFCore().
var auditContext = new StaticAuditContext("console-runner", ipToken: null);
var interceptor = new SensitiveDataAuditInterceptor(auditStore, auditContext);

// ──────────────────────────────────────────────────────────────────────────
Section("1. Annotating models with [PersonalData] / [SensitiveData]");
// ──────────────────────────────────────────────────────────────────────────

var customer = new Customer
{
    DataSubjectId = Guid.NewGuid().ToString(),
    Name          = "Joao da Silva",
    Email         = "joao.silva@example.com",
    TaxId         = "123.456.789-09",
    Phone         = "+55 11 99999-8877",
    CreatedAt     = DateTimeOffset.UtcNow,
};

Log.Information("New customer — name: {Name}, email: {Email}",
    customer.Name.MaskName(),
    customer.Email.MaskEmail());

// ──────────────────────────────────────────────────────────────────────────
Section("2. Persisting to SQLite and generating automatic audit trail");
// ──────────────────────────────────────────────────────────────────────────

using (var activity = activitySource.StartActivity("SaveCustomer"))
{
    activity?.SetTag("dataSubjectId", customer.DataSubjectId);

    var saveOptions = new DbContextOptionsBuilder<SampleDbContext>()
        .UseSqlite("Data Source=sensitiveflow-console.db")
        .AddInterceptors(interceptor)
        .Options;

    using var saveCtx = new SampleDbContext(saveOptions);
    await saveCtx.Database.EnsureCreatedAsync();
    saveCtx.Customers.Add(customer);
    await saveCtx.SaveChangesAsync();

    Log.Information("Customer {DataSubjectId} saved", customer.DataSubjectId);
}

// ──────────────────────────────────────────────────────────────────────────
Section("3. Querying the durable audit trail from SQLite");
// ──────────────────────────────────────────────────────────────────────────

var auditRecords = await auditStore.QueryByDataSubjectAsync(customer.DataSubjectId);
Console.WriteLine($"  Audit records for {customer.DataSubjectId}:");
foreach (var r in auditRecords)
{
    Console.WriteLine($"    [{r.Timestamp:u}] {r.Operation,-10} {r.Entity}.{r.Field,-12} actor={r.ActorId ?? "-"}");
}

// ──────────────────────────────────────────────────────────────────────────
Section("4. IP pseudonymization — token survives to SQLite");
// ──────────────────────────────────────────────────────────────────────────

var rawIp   = "192.168.100.42";
var ipToken = await pseudonymizer.PseudonymizeAsync(rawIp);

Log.Information("Request from IP token {IpToken} (raw IP never logged)", ipToken[..8] + "...");

// Emit audit record with pseudonymized IP — raw IP never stored.
await auditStore.AppendAsync(new AuditRecord
{
    DataSubjectId  = customer.DataSubjectId,
    Entity         = nameof(Customer),
    Field          = "*",
    Operation      = AuditOperation.Access,
    ActorId        = "api-gateway",
    IpAddressToken = ipToken,
});

// In a security investigation, recover the IP from the durable token store:
var resolvedIp = await pseudonymizer.ReverseAsync(ipToken);
Console.WriteLine($"  Token {ipToken[..8]}... resolved to: {resolvedIp}");

// ──────────────────────────────────────────────────────────────────────────
Section("5. Masking and anonymization");
// ──────────────────────────────────────────────────────────────────────────

Console.WriteLine($"  Name masked  : {customer.Name.MaskName()}");
Console.WriteLine($"  Email masked : {customer.Email.MaskEmail()}");
Console.WriteLine($"  Phone masked : {customer.Phone.MaskPhone()}");
Console.WriteLine($"  TaxId anon.  : {new BrazilianTaxIdAnonymizer().Anonymize(customer.TaxId)}");

// ──────────────────────────────────────────────────────────────────────────
Section("6. Retention evaluation");
// ──────────────────────────────────────────────────────────────────────────

var retentionEvaluator = new RetentionEvaluator(Enumerable.Empty<SensitiveFlow.Retention.Contracts.IRetentionExpirationHandler>());

// Not expired: should not throw.
try
{
    await retentionEvaluator.EvaluateAsync(customer, customer.CreatedAt);
    Console.WriteLine("  Retention OK — not expired.");
}
catch (Exception ex)
{
    Console.WriteLine($"  Retention expired: {ex.Message}");
}

// ──────────────────────────────────────────────────────────────────────────
Section("7. OpenTelemetry trace exported");
// ──────────────────────────────────────────────────────────────────────────

using (var span = activitySource.StartActivity("AuditQueryDemo"))
{
    span?.SetTag("subject", customer.DataSubjectId);
    var all = await auditStore.QueryAsync();
    span?.SetTag("records.count", all.Count);
    Console.WriteLine($"  Total audit records in store: {all.Count}");
}

Log.Information("Console sample finished.");
await Log.CloseAndFlushAsync();

// ── Helpers ────────────────────────────────────────────────────────────────

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ──");
}

// ── Domain model ──────────────────────────────────────────────────────────

public sealed class Customer
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Other)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Phone { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

// ── EF Core DbContext with durable AuditStore and TokenStore ──────────────

public sealed class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AuditRecordEntity> AuditEntries => Set<AuditRecordEntity>();
    public DbSet<TokenMappingEntity> TokenMappings => Set<TokenMappingEntity>();

    public IAuditStore AuditStore => new EfCoreAuditStore(this);
    public ITokenStore TokenStore => new EfCoreTokenStore(this);
}

public sealed class AuditRecordEntity
{
    public int Id { get; set; }
    public string RecordId { get; set; } = string.Empty;
    public string DataSubjectId { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public int Operation { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? ActorId { get; set; }
    public string? IpAddressToken { get; set; }
    public string? Details { get; set; }
}

public sealed class TokenMappingEntity
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

// ── Durable IAuditStore backed by EF Core / SQLite ────────────────────────

public sealed class EfCoreAuditStore : IAuditStore
{
    private readonly SampleDbContext _db;
    public EfCoreAuditStore(SampleDbContext db) => _db = db;

    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        _db.AuditEntries.Add(new AuditRecordEntity
        {
            RecordId      = record.Id,
            DataSubjectId = record.DataSubjectId,
            Entity        = record.Entity,
            Field         = record.Field,
            Operation     = (int)record.Operation,
            Timestamp     = record.Timestamp,
            ActorId       = record.ActorId,
            IpAddressToken = record.IpAddressToken,
            Details       = record.Details,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditEntries.AsQueryable();
        if (from.HasValue) { query = query.Where(r => r.Timestamp >= from.Value); }
        if (to.HasValue)   { query = query.Where(r => r.Timestamp <= to.Value); }
        var rows = await query.OrderBy(r => r.Timestamp).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditEntries.Where(r => r.DataSubjectId == dataSubjectId);
        if (from.HasValue) { query = query.Where(r => r.Timestamp >= from.Value); }
        if (to.HasValue)   { query = query.Where(r => r.Timestamp <= to.Value); }
        var rows = await query.OrderBy(r => r.Timestamp).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return rows.Select(ToRecord).ToList();
    }

    private static AuditRecord ToRecord(AuditRecordEntity e) => new()
    {
        Id            = e.RecordId,
        DataSubjectId = e.DataSubjectId,
        Entity        = e.Entity,
        Field         = e.Field,
        Operation     = (AuditOperation)e.Operation,
        Timestamp     = e.Timestamp,
        ActorId       = e.ActorId,
        IpAddressToken = e.IpAddressToken,
        Details       = e.Details,
    };
}

// ── Durable ITokenStore backed by EF Core / SQLite ────────────────────────

public sealed class EfCoreTokenStore : ITokenStore
{
    private readonly SampleDbContext _db;
    public EfCoreTokenStore(SampleDbContext db) => _db = db;

    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        var existing = await _db.TokenMappings
            .FirstOrDefaultAsync(t => t.Value == value, cancellationToken);

        if (existing is not null)
        {
            return existing.Token;
        }

        var token = Guid.NewGuid().ToString();
        _db.TokenMappings.Add(new TokenMappingEntity { Value = value, Token = token });
        await _db.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var mapping = await _db.TokenMappings
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        if (mapping is null)
        {
            throw new KeyNotFoundException($"Token '{token}' not found in the store.");
        }

        return mapping.Value;
    }
}

// ── Static IAuditContext for console (no HTTP context) ────────────────────

public sealed class StaticAuditContext : IAuditContext
{
    public StaticAuditContext(string actorId, string? ipToken)
    {
        ActorId        = actorId;
        IpAddressToken = ipToken;
    }

    public string? ActorId { get; }
    public string? IpAddressToken { get; }
}
