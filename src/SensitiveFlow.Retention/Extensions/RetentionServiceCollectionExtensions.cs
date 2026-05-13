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
        services.AddScoped<RetentionEvaluator>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IRetentionExpirationHandler"/>.
    /// </summary>
    public static IServiceCollection AddRetentionHandler<THandler>(this IServiceCollection services)
        where THandler : class, IRetentionExpirationHandler
    {
        services.AddScoped<IRetentionExpirationHandler, THandler>();
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

    /// <summary>
    /// Registers <see cref="RetentionSchedulerHostedService"/> to automatically evaluate
    /// retention policies on a background schedule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scheduler runs on a configurable interval (default: 1 hour) and queries all DbSet
    /// properties in the specified DbContext. Entities with expired
    /// <see cref="SensitiveFlow.Core.Attributes.RetentionDataAttribute"/> fields are
    /// automatically anonymized or marked for deletion according to policy.
    /// </para>
    /// <para>
    /// <b>Prerequisite:</b> Call <see cref="AddRetentionExecutor"/> first to register
    /// <see cref="RetentionExecutor"/>.
    /// </para>
    /// <para>
    /// <b>Usage:</b>
    /// <code>
    /// services.AddRetentionExecutor();
    /// services.AddRetentionScheduler&lt;MyAppDbContext&gt;(options =>
    /// {
    ///     options.Interval = TimeSpan.FromHours(1);
    ///     options.InitialDelay = TimeSpan.FromMinutes(5);
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    /// <typeparam name="TDbContext">The DbContext type containing entities with retention policies.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for scheduler options.</param>
    public static IServiceCollection AddRetentionScheduler<TDbContext>(
        this IServiceCollection services,
        Action<RetentionSchedulerOptions>? configure = null)
        where TDbContext : notnull
    {
        var options = new RetentionSchedulerOptions { DbContextType = typeof(TDbContext) };
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<RetentionSchedulerHostedService>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="RetentionSchedulerHostedService"/> with a specific DbContext type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dbContextType">The DbContext type containing entities with retention policies.</param>
    /// <param name="configure">Optional configuration callback for scheduler options.</param>
    public static IServiceCollection AddRetentionScheduler(
        this IServiceCollection services,
        Type dbContextType,
        Action<RetentionSchedulerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(dbContextType);

        var options = new RetentionSchedulerOptions { DbContextType = dbContextType };
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<RetentionSchedulerHostedService>();
        return services;
    }
}
