using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensitiveFlow.Audit.EFCore.Entities;

namespace SensitiveFlow.Audit.EFCore.Configuration;

/// <summary>
/// EF Core configuration for <see cref="AuditRecordEntity"/>. Provides indexes that match the
/// public query surface of <see cref="SensitiveFlow.Core.Interfaces.IAuditStore"/>:
/// <see cref="AuditRecordEntity.Timestamp"/> (range), <see cref="AuditRecordEntity.DataSubjectId"/>
/// (subject lookups) and <see cref="AuditRecordEntity.RecordId"/> (uniqueness).
/// </summary>
public sealed class AuditRecordEntityTypeConfiguration : IEntityTypeConfiguration<AuditRecordEntity>
{
    /// <summary>Default table name when none is specified.</summary>
    public const string DefaultTableName = "SensitiveFlow_AuditRecords";

    private readonly string _tableName;
    private readonly string? _schema;

    /// <summary>Initializes the configuration with the given table name and no schema.</summary>
    public AuditRecordEntityTypeConfiguration(string tableName = DefaultTableName)
        : this(tableName, null)
    {
    }

    /// <summary>
    /// Initializes the configuration with a custom table name and optional schema.
    /// </summary>
    /// <param name="tableName">Table name. Defaults to <see cref="DefaultTableName"/>.</param>
    /// <param name="schema">Optional schema. Leave <c>null</c> for providers without schemas (e.g. SQLite).</param>
    public AuditRecordEntityTypeConfiguration(string tableName, string? schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _tableName = tableName;
        _schema = string.IsNullOrWhiteSpace(schema) ? null : schema;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditRecordEntity> builder)
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

        builder.Property(e => e.RecordId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.DataSubjectId).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Entity).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Field).IsRequired().HasMaxLength(256);
        builder.Property(e => e.ActorId).HasMaxLength(256);
        builder.Property(e => e.IpAddressToken).HasMaxLength(128);
        builder.Property(e => e.Details).HasMaxLength(2048);
        builder.Property(e => e.Timestamp)
            .HasConversion(
                v => v.UtcDateTime,
                v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));

        builder.HasIndex(e => e.RecordId).IsUnique().HasDatabaseName("IX_SensitiveFlow_AuditRecords_RecordId");
        builder.HasIndex(e => e.Timestamp).HasDatabaseName("IX_SensitiveFlow_AuditRecords_Timestamp");
        builder.HasIndex(e => new { e.DataSubjectId, e.Timestamp })
               .HasDatabaseName("IX_SensitiveFlow_AuditRecords_DataSubjectId_Timestamp");
    }
}
