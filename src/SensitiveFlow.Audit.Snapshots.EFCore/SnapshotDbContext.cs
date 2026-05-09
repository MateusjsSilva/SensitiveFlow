using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Audit.Snapshots.EFCore.Configuration;
using SensitiveFlow.Audit.Snapshots.EFCore.Entities;

namespace SensitiveFlow.Audit.Snapshots.EFCore;

/// <summary>
/// Standalone <see cref="DbContext"/> dedicated to SensitiveFlow audit snapshot storage.
/// Use this when you want snapshots to live in their own database — recommended for production
/// so the snapshot log survives even if the primary application database is restored from a backup.
/// </summary>
public class SnapshotDbContext : DbContext
{
    /// <summary>Initializes a new instance.</summary>
    public SnapshotDbContext(DbContextOptions<SnapshotDbContext> options) : base(options) { }

    /// <summary>Audit snapshots persisted by <see cref="Stores.EfCoreAuditSnapshotStore{TContext}"/>.</summary>
    public DbSet<AuditSnapshotEntity> AuditSnapshots => Set<AuditSnapshotEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AuditSnapshotEntityTypeConfiguration());
    }
}
