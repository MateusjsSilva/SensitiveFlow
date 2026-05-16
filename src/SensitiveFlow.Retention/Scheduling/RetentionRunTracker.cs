using System.Collections.Concurrent;

namespace SensitiveFlow.Retention.Scheduling;

/// <summary>
/// Thread-safe in-memory implementation of retention run tracking.
/// </summary>
public class RetentionRunTracker : IRetentionRunTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRuns = new();

    /// <summary>
    /// Gets the last run time for a given policy key.
    /// </summary>
    public DateTimeOffset? GetLastRunAt(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        return _lastRuns.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Marks a policy as having run successfully at the given time.
    /// </summary>
    public void MarkRanAt(string key, DateTimeOffset at)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        _lastRuns[key] = at;
    }
}
