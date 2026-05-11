using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.HealthChecks.Checks;

/// <summary>Health check for audit outbox configuration.</summary>
public sealed class AuditOutboxHealthCheck : IHealthCheck
{
    private readonly IAuditOutbox _outbox;
    private readonly IHostEnvironment? _environment;

    /// <summary>Initializes a new instance.</summary>
    public AuditOutboxHealthCheck(IAuditOutbox outbox, IHostEnvironment? environment = null)
    {
        _outbox = outbox;
        _environment = environment;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isInMemory = _outbox.GetType().FullName == "SensitiveFlow.Audit.Outbox.InMemoryAuditOutbox";
        if (isInMemory && _environment?.IsDevelopment() == false)
        {
            return Task.FromResult(HealthCheckResult.Degraded("In-memory audit outbox is configured outside Development."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Audit outbox resolved."));
    }
}
