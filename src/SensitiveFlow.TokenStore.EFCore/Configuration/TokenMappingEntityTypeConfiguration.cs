using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensitiveFlow.TokenStore.EFCore.Entities;

namespace SensitiveFlow.TokenStore.EFCore.Configuration;

/// <summary>
/// EF Core configuration for <see cref="TokenMappingEntity"/>.
/// Provides a unique index on <see cref="TokenMappingEntity.Value"/> for concurrency-safe
/// GetOrCreate semantics and an index on <see cref="TokenMappingEntity.Token"/> for fast resolution.
/// </summary>
public sealed class TokenMappingEntityTypeConfiguration : IEntityTypeConfiguration<TokenMappingEntity>
{
    private readonly string _tableName;

    /// <summary>Initializes the configuration with the given table name.</summary>
    public TokenMappingEntityTypeConfiguration(string tableName = "SensitiveFlow_TokenMappings")
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name must not be empty.", nameof(tableName));
        }
        _tableName = tableName;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TokenMappingEntity> builder)
    {
        builder.ToTable(_tableName);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Value).IsRequired().HasMaxLength(512);
        builder.Property(e => e.Token).IsRequired().HasMaxLength(128);

        // Unique index on Value enables concurrency-safe GetOrCreate:
        // two concurrent callers racing for the same value will see one
        // succeed and the other catch DbUpdateException, then recover
        // by reading the winner's token.
        builder.HasIndex(e => e.Value)
               .IsUnique()
               .HasDatabaseName("IX_SensitiveFlow_TokenMappings_Value");

        builder.HasIndex(e => e.Token)
               .HasDatabaseName("IX_SensitiveFlow_TokenMappings_Token");
    }
}
