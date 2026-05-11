using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Outbox.Stores;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Audit.EFCore.Outbox.Extensions;

/// <summary>
/// DI extensions for the EF Core durable audit outbox.
/// </summary>
public static class OutboxEFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EfCoreAuditOutbox"/> as the durable <see cref="IDurableAuditOutbox"/>
    /// and wires it with <see cref="AuditOutboxDispatcher"/> for automatic background polling.
    /// </summary>
    /// <remarks>
    /// Call this <b>after</b> AddEfCoreAuditStore() or a similar audit store registration.
    /// The outbox shares the same <see cref="AuditDbContext"/>,
    /// ensuring that audit writes and outbox enqueuing happen in a single transaction.
    /// </remarks>
    public static IServiceCollection AddEfCoreAuditOutbox(
        this IServiceCollection services,
        Action<AuditOutboxDispatcherOptions>? configureDispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the durable outbox
        services.AddSingleton<IDurableAuditOutbox>(sp =>
            new EfCoreAuditOutbox(sp.GetRequiredService<IDbContextFactory<AuditDbContext>>()));

        services.AddSingleton<IAuditOutbox>(sp => (IAuditOutbox)sp.GetRequiredService<IDurableAuditOutbox>());

        // Register the dispatcher
        var dispatcherOptions = new AuditOutboxDispatcherOptions();
        configureDispatcher?.Invoke(dispatcherOptions);
        services.AddSingleton(dispatcherOptions);
        services.AddHostedService<AuditOutboxDispatcher>();

        return services;
    }
}
