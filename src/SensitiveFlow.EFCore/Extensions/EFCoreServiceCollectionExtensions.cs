using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.Context;
using SensitiveFlow.EFCore.Interceptors;

namespace SensitiveFlow.EFCore.Extensions;

/// <summary>
/// Extension methods for registering SensitiveFlow EF Core services.
/// </summary>
public static class EFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="SensitiveDataAuditInterceptor"/> as a singleton.
    /// <para>
    /// <see cref="IAuditContext"/> is registered as <see cref="NullAuditContext"/> only when
    /// no prior registration exists, so calling <c>AddSensitiveFlowAspNetCore()</c> before or
    /// after this method always wins.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSensitiveFlowEFCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IAuditContext>(NullAuditContext.Instance);
        services.AddSingleton<SensitiveDataAuditInterceptor>();
        return services;
    }

    /// <summary>
    /// Replaces the registered <see cref="IAuditContext"/> with a custom scoped implementation.
    /// Call this after <see cref="AddSensitiveFlowEFCore"/> to override the default
    /// <see cref="NullAuditContext"/> with an HTTP-aware or custom context.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowAuditContext<TContext>(this IServiceCollection services)
        where TContext : class, IAuditContext
    {
        services.AddScoped<IAuditContext, TContext>();
        return services;
    }
}
