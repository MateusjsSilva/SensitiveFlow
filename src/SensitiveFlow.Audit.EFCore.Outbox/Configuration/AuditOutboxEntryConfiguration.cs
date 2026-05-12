using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensitiveFlow.Audit.EFCore.Outbox.Entities;

namespace SensitiveFlow.Audit.EFCore.Outbox.Configuration;

/// <summary>
/// EF Core model configuration for <see cref="AuditOutboxEntryEntity"/>.
/// </summary>
/// <remarks>
/// Default table name is <c>AuditOutboxEntries</c> with no schema, ensuring compatibility
/// with providers that do not support schemas (e.g. SQLite). Override the table name or
/// schema via the constructor if you need a different layout.
/// </remarks>
public sealed class AuditOutboxEntryConfiguration : IEntityTypeConfiguration<AuditOutboxEntryEntity>
{
    /// <summary>Default table name when none is specified.</summary>
    public const string DefaultTableName = "AuditOutboxEntries";

    private readonly string _tableName;
    private readonly string? _schema;

    /// <summary>Creates a configuration with the default table name and no schema.</summary>
    public AuditOutboxEntryConfiguration()
        : this(DefaultTableName, null)
    {
    }

    /// <summary>Creates a configuration with a custom table name and optional schema.</summary>
    /// <param name="tableName">Table name. Defaults to <see cref="DefaultTableName"/>.</param>
    /// <param name="schema">Optional schema. Leave <c>null</c> for providers without schemas (e.g. SQLite).</param>
    public AuditOutboxEntryConfiguration(string tableName, string? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        _tableName = tableName;
        _schema = string.IsNullOrWhiteSpace(schema) ? null : schema;
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditOutboxEntryEntity> builder)
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

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.AuditRecordId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Payload)
            .IsRequired();

        builder.Property(e => e.Attempts)
            .HasDefaultValue(0);

        builder.Property(e => e.EnqueuedAt)
            .IsRequired();

        // Provider-agnostic ordering column: ticks (long) sorts identically on every backend.
        builder.Property(e => e.EnqueuedAtTicks)
            .IsRequired();

        builder.Property(e => e.LastAttemptAt);

        builder.Property(e => e.LastError)
            .HasMaxLength(512);

        builder.Property(e => e.IsProcessed)
            .HasDefaultValue(false);

        builder.Property(e => e.ProcessedAt);

        builder.Property(e => e.IsDeadLettered)
            .HasDefaultValue(false);

        builder.Property(e => e.DeadLetterReason)
            .HasMaxLength(512);

        // Indexes for querying pending/dead-lettered entries
        builder.HasIndex(e => new { e.IsProcessed, e.IsDeadLettered });
        builder.HasIndex(e => e.IsDeadLettered);
        builder.HasIndex(e => e.EnqueuedAtTicks);
    }
}
