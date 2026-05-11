namespace SensitiveFlow.Core.Models;

/// <summary>
/// Durable outbox entry that tracks delivery attempts for an audit record.
/// </summary>
public sealed record AuditOutboxEntry
{
    /// <summary>Unique identifier of the outbox entry.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Audit record to deliver.</summary>
    public required AuditRecord Record { get; init; }

    /// <summary>Number of dispatch attempts.</summary>
    public int Attempts { get; init; }

    /// <summary>Timestamp when the entry was enqueued.</summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp of the latest dispatch attempt, when any.</summary>
    public DateTimeOffset? LastAttemptAt { get; init; }

    /// <summary>Latest dispatch error, when any.</summary>
    public string? LastError { get; init; }
}
