using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Stores;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Audit.Extensions;

/// <summary>
/// Extension methods for registering audit services.
/// </summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory <see cref="IAuditStore"/> as a singleton.
    /// Suitable for tests and development. For production, replace with a durable implementation.
    /// </summary>
    public static IServiceCollection AddInMemoryAuditStore(this IServiceCollection services)
    {
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();
        return services;
    }
}
