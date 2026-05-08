using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace MinimalApi.Sample.Infrastructure;

// ── Domain model ─────────────────────────────────────────────────────────

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

public sealed class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AuditRecordEntity> AuditEntries => Set<AuditRecordEntity>();
    public DbSet<TokenMappingEntity> TokenMappings => Set<TokenMappingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique index — concurrent GetOrCreateToken inserts surface as DbUpdateException
        // and the store recovers by re-reading the row that won.
        modelBuilder.Entity<TokenMappingEntity>()
            .HasIndex(t => t.Value)
            .IsUnique();
    }
}

public sealed class EfCoreAuditStore : IAuditStore
{
    private readonly SampleDbContext _db;
    public EfCoreAuditStore(SampleDbContext db) => _db = db;

    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        _db.AuditEntries.Add(new AuditRecordEntity
        {
            RecordId       = record.Id,
            DataSubjectId  = record.DataSubjectId,
            Entity         = record.Entity,
            Field          = record.Field,
            Operation      = (int)record.Operation,
            Timestamp      = record.Timestamp,
            ActorId        = record.ActorId,
            IpAddressToken = record.IpAddressToken,
            Details        = record.Details,
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
        return await Project(query.OrderBy(r => r.Timestamp).Skip(skip).Take(take), cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditEntries.Where(r => r.DataSubjectId == dataSubjectId);
        if (from.HasValue) { query = query.Where(r => r.Timestamp >= from.Value); }
        if (to.HasValue)   { query = query.Where(r => r.Timestamp <= to.Value); }
        return await Project(query.OrderBy(r => r.Timestamp).Skip(skip).Take(take), cancellationToken);
    }

    private static async Task<IReadOnlyList<AuditRecord>> Project(
        IQueryable<AuditRecordEntity> query, CancellationToken ct)
    {
        var rows = await query.ToListAsync(ct);
        return rows.Select(e => new AuditRecord
        {
            Id             = e.RecordId,
            DataSubjectId  = e.DataSubjectId,
            Entity         = e.Entity,
            Field          = e.Field,
            Operation      = (AuditOperation)e.Operation,
            Timestamp      = e.Timestamp,
            ActorId        = e.ActorId,
            IpAddressToken = e.IpAddressToken,
            Details        = e.Details,
        }).ToList();
    }
}

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

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return token;
        }
        catch (DbUpdateException)
        {
            _db.Entry(_db.TokenMappings.Local.First(t => t.Value == value)).State = EntityState.Detached;

            var winner = await _db.TokenMappings
                .AsNoTracking()
                .FirstAsync(t => t.Value == value, cancellationToken);
            return winner.Token;
        }
    }

    public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var mapping = await _db.TokenMappings
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        return mapping?.Value
            ?? throw new KeyNotFoundException($"Token '{token}' not found in the store.");
    }
}
