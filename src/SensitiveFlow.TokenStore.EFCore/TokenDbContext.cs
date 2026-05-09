using Microsoft.EntityFrameworkCore;
using SensitiveFlow.TokenStore.EFCore.Configuration;
using SensitiveFlow.TokenStore.EFCore.Entities;

namespace SensitiveFlow.TokenStore.EFCore;

/// <summary>
/// Standalone <see cref="DbContext"/> dedicated to SensitiveFlow token storage. Use this when you
/// want token mappings to live in their own database — recommended for production so token
/// mappings survive even if the primary application database is restored from a backup.
/// </summary>
public class TokenDbContext : DbContext
{
    /// <summary>Initializes a new instance.</summary>
    public TokenDbContext(DbContextOptions<TokenDbContext> options) : base(options) { }

    /// <summary>Token mappings persisted by <see cref="Stores.EfCoreTokenStore{TContext}"/>.</summary>
    public DbSet<TokenMappingEntity> TokenMappings => Set<TokenMappingEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new TokenMappingEntityTypeConfiguration());
    }
}
