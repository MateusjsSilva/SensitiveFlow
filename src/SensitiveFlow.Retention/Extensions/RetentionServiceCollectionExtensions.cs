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

    /// <summary>
    /// Registers <see cref="RetentionExecutor"/> — the imperative variant of
    /// <see cref="RetentionEvaluator"/> that mutates expired fields in place when their
    /// <see cref="SensitiveFlow.Core.Enums.RetentionPolicy"/> is <c>AnonymizeOnExpiration</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for executor options.</param>
    public static IServiceCollection AddRetentionExecutor(
        this IServiceCollection services,
        Action<RetentionExecutorOptions>? configure = null)
    {
        var options = new RetentionExecutorOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<RetentionExecutor>(sp => new RetentionExecutor(options));
        return services;
    }
}
