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
    private readonly string _tableName;

    /// <summary>Initializes the configuration with the given table name.</summary>
    public AuditRecordEntityTypeConfiguration(string tableName = "SensitiveFlow_AuditRecords")
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name must not be empty.", nameof(tableName));
        }
        _tableName = tableName;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditRecordEntity> builder)
    {
        builder.ToTable(_tableName);

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
