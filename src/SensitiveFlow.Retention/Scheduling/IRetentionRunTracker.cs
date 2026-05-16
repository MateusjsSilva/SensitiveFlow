namespace SensitiveFlow.Retention.Scheduling;

/// <summary>
/// Tracks the last successful retention run per policy to enable incremental scheduling.
/// </summary>
public interface IRetentionRunTracker
{
    /// <summary>
    /// Gets the last run time for a given policy key.
    /// </summary>
    /// <param name="key">The policy identifier key.</param>
    /// <returns>The last run timestamp, or null if never run.</returns>
    DateTimeOffset? GetLastRunAt(string key);

    /// <summary>
    /// Marks a policy as having run successfully at the given time.
    /// </summary>
    /// <param name="key">The policy identifier key.</param>
    /// <param name="at">The run timestamp.</param>
    void MarkRanAt(string key, DateTimeOffset at);
}
