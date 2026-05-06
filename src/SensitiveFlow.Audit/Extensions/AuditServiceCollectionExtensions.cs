using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Audit.Extensions;

/// <summary>
/// Extension methods for registering audit services.
/// </summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers a custom <see cref="IAuditStore"/> implementation as scoped.
    /// </summary>
    /// <typeparam name="TStore">
    /// Your <see cref="IAuditStore"/> implementation backed by a durable sink
    /// (SQL via EF Core, MongoDB, Azure Table Storage, etc.).
    /// Audit records must survive process restarts — an in-memory store is not suitable for production.
    /// </typeparam>
    /// <example>
    /// <code>
    /// // SQL via EF Core:
    /// builder.Services.AddAuditStore&lt;EfCoreAuditStore&gt;();
    ///
    /// // Or register manually with a factory:
    /// builder.Services.AddScoped&lt;IAuditStore&gt;(sp =>
    ///     new EfCoreAuditStore(sp.GetRequiredService&lt;AuditDbContext&gt;()));
    /// </code>
    /// </example>
    public static IServiceCollection AddAuditStore<TStore>(this IServiceCollection services)
        where TStore : class, IAuditStore
    {
        services.AddScoped<IAuditStore, TStore>();
        return services;
    }
}
