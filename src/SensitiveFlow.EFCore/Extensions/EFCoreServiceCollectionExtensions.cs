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
    /// Registers the <see cref="SensitiveDataAuditInterceptor"/> as scoped.
    /// <para>
    /// <see cref="IAuditContext"/> is registered as <see cref="NullAuditContext"/> only when
    /// no prior registration exists, so calling <c>AddSensitiveFlowAspNetCore()</c> before or
    /// after this method always wins.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSensitiveFlowEFCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IAuditContext>(NullAuditContext.Instance);
        services.AddScoped<SensitiveDataAuditInterceptor>();
        return services;
    }
}
