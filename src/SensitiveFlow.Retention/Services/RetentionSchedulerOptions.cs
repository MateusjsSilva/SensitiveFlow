namespace SensitiveFlow.Retention.Services;

/// <summary>
/// Configuration options for <see cref="RetentionSchedulerHostedService"/>.
/// </summary>
public sealed class RetentionSchedulerOptions
{
    /// <summary>
    /// The DbContext type to query for entities with retention policies.
    /// </summary>
    public required Type DbContextType { get; set; }

    /// <summary>
    /// The interval between retention evaluation cycles. Defaults to 1 hour.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Optional delay before the first evaluation cycle runs. Useful to allow
    /// the application to stabilize before starting background operations.
    /// Defaults to 0 (no delay).
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Maximum number of entities to process in a single cycle. If set, retention
    /// evaluation is batched. Use 0 for unlimited (processes all entities).
    /// Defaults to 0 (unlimited).
    /// </summary>
    public int MaxBatchSize { get; set; } = 0;

    /// <summary>
    /// If true, exceptions during retention evaluation do not stop the scheduler.
    /// If false, any unhandled exception stops the background service.
    /// Defaults to true for resilience.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;
}
