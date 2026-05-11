namespace SensitiveFlow.Audit.EFCore.Outbox.Entities;

/// <summary>
/// EF Core persistence entity for durable audit outbox entries.
/// Tracks delivery state and attempts separately from the immutable Core model.
/// </summary>
public sealed class AuditOutboxEntryEntity
{
    /// <summary>Unique stable identifier for this outbox entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The audit record ID being delivered.</summary>
    public string AuditRecordId { get; set; } = string.Empty;

    /// <summary>Serialized audit record payload (JSON).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>How many times delivery has been attempted.</summary>
    public int Attempts { get; set; }

    /// <summary>When the entry was enqueued (UTC).</summary>
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the last delivery attempt was made (UTC), or null if never attempted.</summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>The most recent error message from delivery, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>Whether this entry has been successfully delivered and marked processed.</summary>
    public bool IsProcessed { get; set; }

    /// <summary>When the entry was marked as processed, or null if still pending.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Whether this entry has been moved to dead-letter after max retries.</summary>
    public bool IsDeadLettered { get; set; }

    /// <summary>Reason for dead-lettering, if applicable.</summary>
    public string? DeadLetterReason { get; set; }
}
