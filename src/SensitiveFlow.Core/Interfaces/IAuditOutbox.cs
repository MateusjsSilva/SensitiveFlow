using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Application-owned outbox abstraction for durable audit delivery.
/// </summary>
public interface IAuditOutbox
{
    /// <summary>Enqueues an audit record for later delivery.</summary>
    Task EnqueueAsync(AuditRecord record, CancellationToken cancellationToken = default);
}

