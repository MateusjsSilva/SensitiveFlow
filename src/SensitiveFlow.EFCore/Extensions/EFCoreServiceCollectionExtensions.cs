using Microsoft.Extensions.DependencyInjection;
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
    /// Registers the <see cref="SensitiveDataAuditInterceptor"/> and a <see cref="NullAuditContext"/>
    /// as the default <see cref="IAuditContext"/>.
    /// <para>
    /// Call <see cref="AddSensitiveFlowAuditContext{TContext}"/> afterwards to replace
    /// <see cref="NullAuditContext"/> with an HTTP-aware implementation.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSensitiveFlowEFCore(this IServiceCollection services)
    {
        services.AddSingleton<IAuditContext>(NullAuditContext.Instance);
        services.AddSingleton<SensitiveDataAuditInterceptor>();
        return services;
    }

    /// <summary>
    /// Replaces the registered <see cref="IAuditContext"/> with a custom scoped implementation.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowAuditContext<TContext>(this IServiceCollection services)
        where TContext : class, IAuditContext
    {
        services.AddScoped<IAuditContext, TContext>();
        return services;
    }
}
