using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Delivers audit outbox entries to an application-owned destination such as a
/// webhook, queue, event bus, or stream.
/// </summary>
public interface IAuditOutboxPublisher
{
    /// <summary>Publishes one audit outbox entry.</summary>
    Task PublishAsync(AuditOutboxEntry entry, CancellationToken cancellationToken = default);
}
