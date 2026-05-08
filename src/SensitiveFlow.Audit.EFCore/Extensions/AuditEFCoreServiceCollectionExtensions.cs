using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.EFCore.Maintenance;
using SensitiveFlow.Audit.EFCore.Stores;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Audit.EFCore.Extensions;

/// <summary>DI extensions for the EF Core audit store.</summary>
public static class AuditEFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EfCoreAuditStore{TContext}"/> as the durable <see cref="IAuditStore"/>
    /// implementation, backed by a dedicated <see cref="AuditDbContext"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">Configures <see cref="DbContextOptionsBuilder"/> (provider, connection, etc.).</param>
    /// <example>
    /// <code>
    /// builder.Services.AddEfCoreAuditStore(opt =>
    ///     opt.UseSqlite(builder.Configuration.GetConnectionString("Audit")));
    /// </code>
    /// </example>
    public static IServiceCollection AddEfCoreAuditStore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction)
    {
        ArgumentNullException.ThrowIfNull(optionsAction);

        services.AddDbContextFactory<AuditDbContext>(optionsAction);
        services.AddSingleton<IAuditStore>(sp =>
            new EfCoreAuditStore<AuditDbContext>(
                sp.GetRequiredService<IDbContextFactory<AuditDbContext>>(),
                static ctx => ctx.AuditRecords));
        services.AddSingleton(sp => new AuditLogRetention<AuditDbContext>(
            sp.GetRequiredService<IDbContextFactory<AuditDbContext>>(),
            static ctx => ctx.AuditRecords));

        return services;
    }

    /// <summary>
    /// Registers <see cref="EfCoreAuditStore{TContext}"/> against an existing user-owned
    /// <typeparamref name="TContext"/> that already has the <c>AuditRecordEntity</c> mapped.
    /// </summary>
    public static IServiceCollection AddEfCoreAuditStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddSingleton<IAuditStore>(sp =>
            new EfCoreAuditStore<TContext>(
                sp.GetRequiredService<IDbContextFactory<TContext>>()));
        services.AddSingleton(sp => new AuditLogRetention<TContext>(
            sp.GetRequiredService<IDbContextFactory<TContext>>()));
        return services;
    }
}
