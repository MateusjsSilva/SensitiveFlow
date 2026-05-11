using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.HealthChecks.Checks;

namespace SensitiveFlow.HealthChecks.Extensions;

/// <summary>
/// Health check builder extensions for SensitiveFlow infrastructure.
/// </summary>
public static class SensitiveFlowHealthCheckBuilderExtensions
{
    /// <summary>Returns the health check builder for fluent SensitiveFlow registration.</summary>
    public static IHealthChecksBuilder AddSensitiveFlowHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddHealthChecks();
    }

    /// <summary>Adds an audit store health check.</summary>
    public static IHealthChecksBuilder AddAuditStoreCheck(
        this IHealthChecksBuilder builder,
        string name = SensitiveFlowDefaults.AuditStoreHealthCheckName,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddCheck<AuditStoreHealthCheck>(name, failureStatus, tags ?? Array.Empty<string>());
    }

    /// <summary>Adds a token store health check.</summary>
    public static IHealthChecksBuilder AddTokenStoreCheck(
        this IHealthChecksBuilder builder,
        string name = SensitiveFlowDefaults.TokenStoreHealthCheckName,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddCheck<TokenStoreHealthCheck>(name, failureStatus, tags ?? Array.Empty<string>());
    }
}
