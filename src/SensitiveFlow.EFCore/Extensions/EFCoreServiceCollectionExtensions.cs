using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.BulkOperations;
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

    /// <summary>
    /// Registers the bulk-operation guard plus the options it consumes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Add this alongside <see cref="AddSensitiveFlowEFCore"/> when the application uses
    /// <c>ExecuteUpdateAsync</c> or <c>ExecuteDeleteAsync</c>. The guard is registered as a
    /// singleton and must be attached to the <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
    /// via <c>DbContextOptionsBuilder.AddInterceptors</c> — typical wire-up:
    /// <code>
    /// services.AddSensitiveBulkOperations();
    /// services.AddDbContext&lt;AppDbContext&gt;((sp, options) =&gt;
    ///     options.AddInterceptors(sp.GetRequiredService&lt;SensitiveBulkOperationsGuardInterceptor&gt;()));
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback.</param>
    public static IServiceCollection AddSensitiveBulkOperations(
        this IServiceCollection services,
        Action<SensitiveBulkOperationsOptions>? configure = null)
    {
        var options = new SensitiveBulkOperationsOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<SensitiveBulkOperationsGuardInterceptor>();
        return services;
    }
}
