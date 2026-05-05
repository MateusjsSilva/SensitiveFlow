namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Provides per-request audit context (actor and IP token).
/// Register a scoped implementation that reads these values from the current
/// application identity or HTTP context.
/// </summary>
public interface IAuditContext
{
    /// <summary>Identifier of the actor performing the operation, or <see langword="null"/> if unknown.</summary>
    string? ActorId { get; }

    /// <summary>
    /// Pseudonymized token for the request IP address, or <see langword="null"/> if unavailable.
    /// Never store a raw IP address here — pseudonymize it first.
    /// </summary>
    string? IpAddressToken { get; }
}
