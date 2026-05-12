using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.AspNetCore.Context;
using SensitiveFlow.AspNetCore.Diagnostics;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering SensitiveFlow ASP.NET Core services.
/// </summary>
public static class AspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HttpAuditContext"/> as the scoped <see cref="IAuditContext"/>,
    /// backed by the HTTP context accessor.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowAspNetCore(
        this IServiceCollection services,
        Action<SensitiveFlowAuditMiddlewareOptions>? configureMiddleware = null)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditContext, HttpAuditContext>();
        services.AddSingleton<SensitiveFlowAspNetCorePipelineDiagnostics>();

        var middlewareOptions = new SensitiveFlowAuditMiddlewareOptions();
        configureMiddleware?.Invoke(middlewareOptions);
        services.AddSingleton(middlewareOptions);

        return services;
    }

    /// <summary>
    /// Adds the <see cref="SensitiveFlowAuditMiddleware"/> to the pipeline.
    /// Place this before authentication and authorization middleware so the IP token
    /// is available throughout the request.
    /// </summary>
    public static IApplicationBuilder UseSensitiveFlowAudit(this IApplicationBuilder app)
    {
        app.ApplicationServices
            .GetService<SensitiveFlowAspNetCorePipelineDiagnostics>()
            ?.MarkAuditMiddlewareRegistered();
        app.UseMiddleware<SensitiveFlowAuditMiddleware>();
        return app;
    }
}
