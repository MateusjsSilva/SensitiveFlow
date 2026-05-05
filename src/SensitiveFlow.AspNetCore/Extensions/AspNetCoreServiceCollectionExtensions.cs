using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.AspNetCore.Context;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering SensitiveFlow ASP.NET Core services.
/// </summary>
public static class AspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HttpAuditContext"/> as the scoped <see cref="IAuditContext"/>,
    /// backed by <see cref="IHttpContextAccessor"/>.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowAspNetCore(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditContext, HttpAuditContext>();
        return services;
    }

    /// <summary>
    /// Adds the <see cref="SensitiveFlowAuditMiddleware"/> to the pipeline.
    /// Place this before authentication and authorization middleware so the IP token
    /// is available throughout the request.
    /// </summary>
    public static IApplicationBuilder UseSensitiveFlowAudit(this IApplicationBuilder app)
    {
        app.UseMiddleware<SensitiveFlowAuditMiddleware>();
        return app;
    }
}
