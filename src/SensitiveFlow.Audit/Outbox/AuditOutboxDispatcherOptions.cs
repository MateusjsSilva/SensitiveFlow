namespace SensitiveFlow.Audit.Outbox;

/// <summary>Options for the hosted audit outbox dispatcher.</summary>
public sealed class AuditOutboxDispatcherOptions
{
    /// <summary>Polling interval. Defaults to one second.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum number of outbox entries dequeued per poll. Defaults to 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Maximum number of delivery attempts before dead-lettering. Defaults to 5.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Backoff strategy between failed attempts.</summary>
    public BackoffStrategy Backoff { get; set; } = BackoffStrategy.Exponential;

    /// <summary>Whether entries should be treated as dead-lettered after max attempts.</summary>
    public bool DeadLetterAfterMax { get; set; } = true;

    /// <summary>
    /// Whether the hosted dispatcher should stop polling after an infrastructure failure.
    /// Defaults to <see langword="true"/> so a missing table, unavailable database, or invalid
    /// connection string does not crash the application host or flood logs.
    /// </summary>
    public bool SuspendOnInfrastructureFailure { get; set; } = true;

    /// <summary>
    /// Delay before retrying after an infrastructure failure when
    /// <see cref="SuspendOnInfrastructureFailure"/> is <see langword="false"/>.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan InfrastructureFailureRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}
