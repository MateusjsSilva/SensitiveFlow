using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensitiveFlow.Audit.Snapshots.EFCore.Entities;

namespace SensitiveFlow.Audit.Snapshots.EFCore.Configuration;

/// <summary>
/// EF Core configuration for <see cref="AuditSnapshotEntity"/>.
/// Provides indexes for aggregate lookups, data-subject queries, and timestamp range scans.
/// </summary>
public sealed class AuditSnapshotEntityTypeConfiguration : IEntityTypeConfiguration<AuditSnapshotEntity>
{
    private readonly string _tableName;

    /// <summary>Initializes the configuration with the given table name.</summary>
    public AuditSnapshotEntityTypeConfiguration(string tableName = "SensitiveFlow_AuditSnapshots")
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name must not be empty.", nameof(tableName));
        }
        _tableName = tableName;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditSnapshotEntity> builder)
    {
        builder.ToTable(_tableName);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.SnapshotId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.DataSubjectId).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Aggregate).IsRequired().HasMaxLength(256);
        builder.Property(e => e.AggregateId).IsRequired().HasMaxLength(256);
        builder.Property(e => e.ActorId).HasMaxLength(256);
        builder.Property(e => e.IpAddressToken).HasMaxLength(128);
        builder.Property(e => e.BeforeJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.AfterJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Timestamp)
            .HasConversion(
                v => v.UtcDateTime,
                v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));

        builder.HasIndex(e => e.SnapshotId).IsUnique()
               .HasDatabaseName("IX_SensitiveFlow_AuditSnapshots_SnapshotId");

        builder.HasIndex(e => new { e.Aggregate, e.AggregateId, e.Timestamp })
               .HasDatabaseName("IX_SensitiveFlow_AuditSnapshots_Aggregate_Timestamp");

        builder.HasIndex(e => new { e.DataSubjectId, e.Timestamp })
               .HasDatabaseName("IX_SensitiveFlow_AuditSnapshots_DataSubjectId_Timestamp");
    }
}
