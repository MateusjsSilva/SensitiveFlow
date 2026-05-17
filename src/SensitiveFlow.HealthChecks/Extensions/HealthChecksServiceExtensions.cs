using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.HealthChecks.Alerting;
using SensitiveFlow.HealthChecks.AuditTracking;
using SensitiveFlow.HealthChecks.DataQuality;
using SensitiveFlow.HealthChecks.PerformanceMetrics;
using SensitiveFlow.HealthChecks.PolicyValidation;

namespace SensitiveFlow.HealthChecks.Extensions;

/// <summary>
/// Extension methods for registering SensitiveFlow health check services.
/// </summary>
public static class HealthChecksServiceExtensions
{
    /// <summary>
    /// Registers retention policy validator.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowRetentionPolicyValidator(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<RetentionPolicyValidator>();

        return services;
    }

    /// <summary>
    /// Registers health check performance collector.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowHealthCheckPerformanceMetrics(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<HealthCheckPerformanceCollector>();

        return services;
    }

    /// <summary>
    /// Registers data quality checker.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowDataQualityChecker(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DataQualityChecker>();

        return services;
    }

    /// <summary>
    /// Registers health alerting policy with optional configuration.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowHealthAlerting(
        this IServiceCollection services,
        Action<HealthAlertingPolicy>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var policy = new HealthAlertingPolicy();
        configure?.Invoke(policy);

        services.AddSingleton(policy);

        return services;
    }

    /// <summary>
    /// Registers audit age tracker with optional configuration.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowAuditAgeTracking(
        this IServiceCollection services,
        Action<AuditAgeTracker>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var tracker = new AuditAgeTracker();
        configure?.Invoke(tracker);

        services.AddSingleton(tracker);

        return services;
    }

    /// <summary>
    /// Registers all SensitiveFlow health check enhancements.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowHealthCheckEnhancements(
        this IServiceCollection services,
        Action<HealthCheckEnhancementsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new HealthCheckEnhancementsOptions();
        configure?.Invoke(options);

        if (options.EnablePolicyValidation)
        {
            services.AddSensitiveFlowRetentionPolicyValidator();
        }

        if (options.EnablePerformanceMetrics)
        {
            services.AddSensitiveFlowHealthCheckPerformanceMetrics();
        }

        if (options.EnableDataQualityChecks)
        {
            services.AddSensitiveFlowDataQualityChecker();
        }

        if (options.EnableAlerting)
        {
            services.AddSensitiveFlowHealthAlerting(options.AlertingConfigure);
        }

        if (options.EnableAuditAgeTracking)
        {
            services.AddSensitiveFlowAuditAgeTracking(options.AuditAgeConfigure);
        }

        return services;
    }
}

/// <summary>
/// Configuration options for health check enhancements.
/// </summary>
public sealed class HealthCheckEnhancementsOptions
{
    /// <summary>Gets or sets whether policy validation is enabled.</summary>
    public bool EnablePolicyValidation { get; set; } = true;

    /// <summary>Gets or sets whether performance metrics are enabled.</summary>
    public bool EnablePerformanceMetrics { get; set; } = true;

    /// <summary>Gets or sets whether data quality checks are enabled.</summary>
    public bool EnableDataQualityChecks { get; set; } = true;

    /// <summary>Gets or sets whether health alerting is enabled.</summary>
    public bool EnableAlerting { get; set; } = false;

    /// <summary>Gets or sets whether audit age tracking is enabled.</summary>
    public bool EnableAuditAgeTracking { get; set; } = true;

    /// <summary>Gets or sets the alerting configuration callback.</summary>
    internal Action<HealthAlertingPolicy>? AlertingConfigure { get; private set; }

    /// <summary>Gets or sets the audit age tracking configuration callback.</summary>
    internal Action<AuditAgeTracker>? AuditAgeConfigure { get; private set; }

    /// <summary>
    /// Configures alerting rules.
    /// </summary>
    public HealthCheckEnhancementsOptions ConfigureAlerting(Action<HealthAlertingPolicy> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnableAlerting = true;
        AlertingConfigure = configure;
        return this;
    }

    /// <summary>
    /// Configures audit age tracking.
    /// </summary>
    public HealthCheckEnhancementsOptions ConfigureAuditAgeTracking(Action<AuditAgeTracker> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        AuditAgeConfigure = configure;
        return this;
    }
}
