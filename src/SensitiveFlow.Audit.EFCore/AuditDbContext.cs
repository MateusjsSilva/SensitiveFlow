using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Audit.EFCore.Configuration;
using SensitiveFlow.Audit.EFCore.Entities;

namespace SensitiveFlow.Audit.EFCore;

/// <summary>
/// Standalone <see cref="DbContext"/> dedicated to SensitiveFlow audit storage. Use this when you
/// want the audit log to live in its own schema/database — recommended for production so the
/// audit log survives even if the primary application database is restored from a backup.
/// </summary>
public class AuditDbContext : DbContext
{
    /// <summary>Initializes a new instance.</summary>
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    /// <summary>Audit records persisted by <see cref="Stores.EfCoreAuditStore{TContext}"/>.</summary>
    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AuditRecordEntityTypeConfiguration());
    }
}
