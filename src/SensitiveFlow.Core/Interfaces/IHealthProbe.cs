namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Optional non-mutating health probe for SensitiveFlow infrastructure implementations.
/// </summary>
public interface IHealthProbe
{
    /// <summary>Verifies the implementation can respond to a lightweight probe.</summary>
    Task ProbeAsync(CancellationToken cancellationToken = default);
}

