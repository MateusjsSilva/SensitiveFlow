using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.EFCore.Context;

/// <summary>
/// No-op implementation of <see cref="IAuditContext"/> used when no context is configured.
/// Audit records emitted with this context will have null actor and IP token.
/// </summary>
public sealed class NullAuditContext : IAuditContext
{
    /// <summary>Singleton instance.</summary>
    public static readonly NullAuditContext Instance = new();

    /// <inheritdoc />
    public string? ActorId => null;

    /// <inheritdoc />
    public string? IpAddressToken => null;
}
