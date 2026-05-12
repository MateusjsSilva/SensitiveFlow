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
    /// <summary>Default table name when none is specified.</summary>
    public const string DefaultTableName = "SensitiveFlow_TokenMappings";

    private readonly string _tableName;
    private readonly string? _schema;

    /// <summary>Initializes the configuration with the given table name and no schema.</summary>
    public TokenMappingEntityTypeConfiguration(string tableName = DefaultTableName)
        : this(tableName, null)
    {
    }

    /// <summary>
    /// Initializes the configuration with a custom table name and optional schema.
    /// </summary>
    /// <param name="tableName">Table name. Defaults to <see cref="DefaultTableName"/>.</param>
    /// <param name="schema">Optional schema. Leave <c>null</c> for providers without schemas (e.g. SQLite).</param>
    public TokenMappingEntityTypeConfiguration(string tableName, string? schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _tableName = tableName;
        _schema = string.IsNullOrWhiteSpace(schema) ? null : schema;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TokenMappingEntity> builder)
    {
        if (_schema is null)
        {
            builder.ToTable(_tableName);
        }
        else
        {
            builder.ToTable(_tableName, _schema);
        }

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
