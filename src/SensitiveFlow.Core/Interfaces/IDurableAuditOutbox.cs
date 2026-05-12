using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Durable audit outbox that supports polling, acknowledgements, and failure tracking.
/// </summary>
public interface IDurableAuditOutbox : IAuditOutbox
{
    /// <summary>Dequeues a batch of pending entries for dispatch.</summary>
    Task<IReadOnlyList<AuditOutboxEntry>> DequeueBatchAsync(
        int max,
        CancellationToken cancellationToken = default);

    /// <summary>Marks entries as processed after successful dispatch.</summary>
    Task MarkProcessedAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Marks one entry as failed with a safe diagnostic message.</summary>
    Task MarkFailedAsync(
        Guid id,
        string error,
        CancellationToken cancellationToken = default);

    /// <summary>Marks one entry as dead-lettered after max retries.</summary>
    Task MarkDeadLetteredAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default);
}
