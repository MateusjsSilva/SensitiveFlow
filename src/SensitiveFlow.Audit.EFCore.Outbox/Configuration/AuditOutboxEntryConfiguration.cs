using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SensitiveFlow.Audit.EFCore.Outbox.Entities;

namespace SensitiveFlow.Audit.EFCore.Outbox.Configuration;

/// <summary>
/// EF Core model configuration for <see cref="AuditOutboxEntryEntity"/>.
/// </summary>
public sealed class AuditOutboxEntryConfiguration : IEntityTypeConfiguration<AuditOutboxEntryEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditOutboxEntryEntity> builder)
    {
        builder.ToTable("AuditOutboxEntries", "sensitiveflow");

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
        builder.HasIndex(e => e.EnqueuedAt);
    }
}
