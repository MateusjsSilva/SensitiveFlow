using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Snapshots.EFCore.Stores;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Audit.Snapshots.EFCore.Extensions;

/// <summary>DI extensions for the EF Core audit snapshot store.</summary>
public static class SnapshotEFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EfCoreAuditSnapshotStore{TContext}"/> as the durable
    /// <see cref="IAuditSnapshotStore"/> implementation, backed by a dedicated
    /// <see cref="SnapshotDbContext"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">Configures <see cref="DbContextOptionsBuilder"/> (provider, connection, etc.).</param>
    /// <example>
    /// <code>
    /// builder.Services.AddEfCoreAuditSnapshotStore(opt =>
    ///     opt.UseSqlServer(builder.Configuration.GetConnectionString("Snapshots")));
    /// </code>
    /// </example>
    public static IServiceCollection AddEfCoreAuditSnapshotStore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction)
    {
        ArgumentNullException.ThrowIfNull(optionsAction);

        services.AddDbContextFactory<SnapshotDbContext>(optionsAction);
        services.AddSingleton<IAuditSnapshotStore>(sp =>
            new EfCoreAuditSnapshotStore<SnapshotDbContext>(
                sp.GetRequiredService<IDbContextFactory<SnapshotDbContext>>(),
                static ctx => ctx.AuditSnapshots));

        return services;
    }

    /// <summary>
    /// Registers <see cref="EfCoreAuditSnapshotStore{TContext}"/> against an existing user-owned
    /// <typeparamref name="TContext"/> that already has the <c>AuditSnapshotEntity</c> mapped.
    /// </summary>
    /// <typeparam name="TContext">Your application's <see cref="DbContext"/> that maps <c>AuditSnapshotEntity</c>.</typeparam>
    /// <example>
    /// <code>
    /// builder.Services.AddEfCoreAuditSnapshotStore&lt;MyAppDbContext&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddEfCoreAuditSnapshotStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddSingleton<IAuditSnapshotStore>(sp =>
            new EfCoreAuditSnapshotStore<TContext>(
                sp.GetRequiredService<IDbContextFactory<TContext>>()));

        return services;
    }
}
