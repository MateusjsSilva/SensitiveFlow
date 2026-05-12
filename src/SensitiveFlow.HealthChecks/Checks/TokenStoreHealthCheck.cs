using Microsoft.Extensions.Diagnostics.HealthChecks;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.HealthChecks.Checks;

/// <summary>
/// Health check for <see cref="ITokenStore"/>.
/// </summary>
public sealed class TokenStoreHealthCheck : IHealthCheck
{
    private readonly ITokenStore _store;

    /// <summary>Initializes a new instance.</summary>
    public TokenStoreHealthCheck(ITokenStore store)
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

            return HealthCheckResult.Healthy("Token store resolved.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Token store probe failed.", ex);
        }
    }
}

