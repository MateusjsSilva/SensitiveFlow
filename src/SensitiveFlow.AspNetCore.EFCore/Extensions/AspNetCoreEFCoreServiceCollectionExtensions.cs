using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.AspNetCore.EFCore.DtoMapping;
using SensitiveFlow.AspNetCore.EFCore.PerformanceMetrics;
using SensitiveFlow.AspNetCore.EFCore.RoleBasedRedaction;

namespace SensitiveFlow.AspNetCore.EFCore.Extensions;

/// <summary>
/// Extension methods for registering SensitiveFlow AspNetCore.EFCore services.
/// </summary>
public static class AspNetCoreEFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers DTO mapping services for automatic entity-to-DTO conversion.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowDtoMapping(
        this IServiceCollection services,
        Action<DtoMappingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DtoMappingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<DtoMapper>();

        return services;
    }

    /// <summary>
    /// Registers role-based redaction configuration.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowRoleBasedRedaction(
        this IServiceCollection services,
        Action<RoleBasedRedactionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new RoleBasedRedactionOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        return services;
    }

    /// <summary>
    /// Registers performance metrics collection for redaction operations.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowRedactionMetrics(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<RedactionMetricsCollector>();

        return services;
    }
}
