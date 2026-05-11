using System.Collections.Concurrent;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Outbox;

/// <summary>
/// In-memory audit outbox implementation for tests, samples, and local development.
/// It is not durable and should not be used as the only production outbox.
/// </summary>
[Obsolete("InMemoryAuditOutbox is for tests/local development only. Use AddEfCoreAuditOutbox() or AddAuditOutbox<TOutbox>() for durable production delivery.", error: false)]
public sealed class InMemoryAuditOutbox : IAuditOutbox
{
    private readonly ConcurrentQueue<AuditRecord> _records = new();

    /// <summary>Gets a point-in-time snapshot of queued audit records.</summary>
    public IReadOnlyList<AuditRecord> Records => _records.ToArray();

    /// <inheritdoc />
    public Task EnqueueAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        _records.Enqueue(record);
        return Task.CompletedTask;
    }
}
