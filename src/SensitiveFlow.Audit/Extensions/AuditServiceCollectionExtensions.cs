using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Audit.Decorators;
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
    /// // SQL via EF Core (dedicated AuditDbContext — preferred):
    /// builder.Services.AddEfCoreAuditStore(opt =>
    ///     opt.UseSqlite(builder.Configuration.GetConnectionString("Audit")));
    ///
    /// // SQL via EF Core (user-owned DbContext that maps AuditRecordEntity):
    /// builder.Services.AddEfCoreAuditStore&lt;MyAppDbContext&gt;();
    ///
    /// // Custom store (any sink):
    /// builder.Services.AddAuditStore&lt;MyCustomAuditStore&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddAuditStore<TStore>(this IServiceCollection services)
        where TStore : class, IAuditStore
    {
        services.AddScoped<IAuditStore, TStore>();
        return services;
    }

    /// <summary>
    /// Wraps the registered <see cref="IAuditStore"/> with <see cref="RetryingAuditStore"/>
    /// so transient append failures (lock contention, network blip) are retried with bounded
    /// exponential backoff before bubbling out to <c>SaveChanges</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for retry options.</param>
    /// <remarks>
    /// Call this <b>after</b> <see cref="AddAuditStore{TStore}"/>. The original registration is
    /// replaced — only the retrying decorator is resolved as <see cref="IAuditStore"/>.
    /// </remarks>
    public static IServiceCollection AddAuditStoreRetry(
        this IServiceCollection services,
        Action<RetryingAuditStoreOptions>? configure = null)
    {
        var options = new RetryingAuditStoreOptions();
        configure?.Invoke(options);

        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore))
            ?? throw new InvalidOperationException(
                $"No {nameof(IAuditStore)} registration was found. Call AddAuditStore<T>() before AddAuditStoreRetry().");

        services.Remove(existing);

        // Re-register the inner store under its own concrete type so the decorator can resolve it.
        // Preserve the inner store's original lifetime so the decorator does not
        // accidentally capture a Singleton inside a Scoped wrapper (or vice-versa),
        // which would trigger DI captive-dependency validation errors.
        if (existing.ImplementationType is not null)
        {
            services.Add(new ServiceDescriptor(existing.ImplementationType, existing.ImplementationType, existing.Lifetime));
            services.Add(new ServiceDescriptor(typeof(IAuditStore),
                sp => new RetryingAuditStore(
                    (IAuditStore)sp.GetRequiredService(existing.ImplementationType),
                    options,
                    sp.GetService<ILogger<RetryingAuditStore>>()),
                existing.Lifetime));
        }
        else if (existing.ImplementationFactory is not null)
        {
            var factory = existing.ImplementationFactory;
            services.Add(new ServiceDescriptor(typeof(IAuditStore),
                sp => new RetryingAuditStore(
                    (IAuditStore)factory(sp),
                    options,
                    sp.GetService<ILogger<RetryingAuditStore>>()),
                existing.Lifetime));
        }
        else if (existing.ImplementationInstance is IAuditStore instance)
        {
            services.AddSingleton<IAuditStore>(new RetryingAuditStore(
                instance,
                options,
                logger: null));
        }

        return services;
    }

    /// <summary>
    /// Wraps the registered <see cref="IAuditStore"/> with <see cref="BufferedAuditStore"/>
    /// so append calls enqueue records into a bounded in-process buffer and a background
    /// worker flushes them to the durable store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for buffer options.</param>
    /// <remarks>
    /// Call this <b>after</b> <see cref="AddAuditStore{TStore}"/>. The original registration is
    /// replaced. Because records are accepted before the durable write completes, a process crash
    /// can lose records that are still in memory.
    /// </remarks>
    public static IServiceCollection AddBufferedAuditStore(
        this IServiceCollection services,
        Action<BufferedAuditStoreOptions>? configure = null)
    {
        var options = new BufferedAuditStoreOptions();
        configure?.Invoke(options);

        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore))
            ?? throw new InvalidOperationException(
                $"No {nameof(IAuditStore)} registration was found. Call AddAuditStore<T>() before AddBufferedAuditStore().");

        services.Remove(existing);

        if (existing.ImplementationType is not null)
        {
            services.Add(new ServiceDescriptor(existing.ImplementationType, existing.ImplementationType, existing.Lifetime));
            services.Add(new ServiceDescriptor(typeof(IAuditStore),
                sp => new BufferedAuditStore(
                    (IAuditStore)sp.GetRequiredService(existing.ImplementationType),
                    options,
                    sp.GetService<ILogger<BufferedAuditStore>>()),
                existing.Lifetime));
        }
        else if (existing.ImplementationFactory is not null)
        {
            var factory = existing.ImplementationFactory;
            services.Add(new ServiceDescriptor(typeof(IAuditStore),
                sp => new BufferedAuditStore(
                    (IAuditStore)factory(sp),
                    options,
                    sp.GetService<ILogger<BufferedAuditStore>>()),
                existing.Lifetime));
        }
        else if (existing.ImplementationInstance is IAuditStore instance)
        {
            services.AddSingleton<IAuditStore>(new BufferedAuditStore(
                instance,
                options,
                logger: null));
        }

        return services;
    }
}
