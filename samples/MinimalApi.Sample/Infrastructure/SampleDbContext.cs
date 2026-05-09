using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;

namespace MinimalApi.Sample.Infrastructure;

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
    public DbSet<TokenMappingEntity> TokenMappings => Set<TokenMappingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TokenMappingEntity>()
            .HasIndex(t => t.Value)
            .IsUnique();
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

        var token = Guid.NewGuid().ToString("N");
        _db.TokenMappings.Add(new TokenMappingEntity { Value = value, Token = token });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return token;
        }
        catch (DbUpdateException)
        {
            var local = _db.TokenMappings.Local.FirstOrDefault(t => t.Value == value);
            if (local is not null)
            {
                _db.Entry(local).State = EntityState.Detached;
            }

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
