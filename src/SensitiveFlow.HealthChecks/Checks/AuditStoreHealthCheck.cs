using Microsoft.Extensions.Diagnostics.HealthChecks;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.HealthChecks.Checks;

/// <summary>
/// Health check for <see cref="IAuditStore"/>.
/// </summary>
public sealed class AuditStoreHealthCheck : IHealthCheck
{
    private readonly IAuditStore _store;

    /// <summary>Initializes a new instance.</summary>
    public AuditStoreHealthCheck(IAuditStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_store is IHealthProbe probe)
            {
                await probe.ProbeAsync(cancellationToken);
            }
            else
            {
                await _store.QueryAsync(take: 1, cancellationToken: cancellationToken);
            }

            return HealthCheckResult.Healthy("Audit store responded.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Audit store probe failed.", ex);
        }
    }
}

