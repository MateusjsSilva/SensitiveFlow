using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Retention.Contracts;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Extensions;

/// <summary>
/// Extension methods for registering retention services.
/// </summary>
public static class RetentionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="RetentionEvaluator"/> and any provided expiration handlers.
    /// </summary>
    public static IServiceCollection AddRetention(this IServiceCollection services)
    {
        services.AddTransient<RetentionEvaluator>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IRetentionExpirationHandler"/>.
    /// </summary>
    public static IServiceCollection AddRetentionHandler<THandler>(this IServiceCollection services)
        where THandler : class, IRetentionExpirationHandler
    {
        services.AddTransient<IRetentionExpirationHandler, THandler>();
        return services;
    }
}
