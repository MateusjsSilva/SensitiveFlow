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
    /// <summary>Default table name when none is specified.</summary>
    public const string DefaultTableName = "SensitiveFlow_AuditSnapshots";

    private readonly string _tableName;
    private readonly string? _schema;

    /// <summary>Initializes the configuration with the given table name and no schema.</summary>
    public AuditSnapshotEntityTypeConfiguration(string tableName = DefaultTableName)
        : this(tableName, null)
    {
    }

    /// <summary>
    /// Initializes the configuration with a custom table name and optional schema.
    /// </summary>
    /// <param name="tableName">Table name. Defaults to <see cref="DefaultTableName"/>.</param>
    /// <param name="schema">Optional schema. Leave <c>null</c> for providers without schemas (e.g. SQLite).</param>
    public AuditSnapshotEntityTypeConfiguration(string tableName, string? schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _tableName = tableName;
        _schema = string.IsNullOrWhiteSpace(schema) ? null : schema;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditSnapshotEntity> builder)
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

        builder.Property(e => e.SnapshotId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.DataSubjectId).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Aggregate).IsRequired().HasMaxLength(256);
        builder.Property(e => e.AggregateId).IsRequired().HasMaxLength(256);
        builder.Property(e => e.ActorId).HasMaxLength(256);
        builder.Property(e => e.IpAddressToken).HasMaxLength(128);
        builder.Property(e => e.BeforeJson);
        builder.Property(e => e.AfterJson);
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
