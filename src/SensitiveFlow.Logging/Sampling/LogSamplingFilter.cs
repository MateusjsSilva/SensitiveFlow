namespace SensitiveFlow.Logging.Sampling;

/// <summary>
/// Filters log entries with sensitive fields based on sampling rate.
/// Reduces log volume in high-throughput scenarios containing sensitive data.
/// </summary>
public sealed class LogSamplingFilter
{
    private readonly double _samplingRate;

    /// <summary>
    /// Initializes a new instance with a sampling rate.
    /// </summary>
    /// <param name="samplingRate">Fraction of logs to keep (0.0–1.0). Default 1.0 (all logs).</param>
    public LogSamplingFilter(double samplingRate = 1.0)
    {
        if (samplingRate < 0.0 || samplingRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(samplingRate), "Must be between 0.0 and 1.0");
        }

        _samplingRate = samplingRate;
    }

    /// <summary>
    /// Determines whether a log entry with redacted fields should be sampled (kept).
    /// </summary>
    /// <param name="hasRedactedFields">Whether the log entry contains redacted sensitive fields.</param>
    /// <returns>True if the entry should be logged, false if it should be dropped.</returns>
    public bool ShouldLog(bool hasRedactedFields)
    {
        if (!hasRedactedFields || _samplingRate >= 1.0)
        {
            return true;
        }

        return Random.Shared.NextDouble() < _samplingRate;
    }

    /// <summary>
    /// Gets the effective sampling rate.
    /// </summary>
    public double SamplingRate => _samplingRate;

    /// <summary>
    /// Checks if sampling is enabled (rate less than 1.0).
    /// </summary>
    public bool IsEnabled => _samplingRate < 1.0;
}
