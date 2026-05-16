namespace SensitiveFlow.Diagnostics.PerformanceBaselines;

/// <summary>
/// Manages performance baselines and detects regressions.
/// </summary>
public sealed class PerformanceBaselineService
{
    private readonly Dictionary<string, PerformanceBaseline> _baselines = new();

    /// <summary>
    /// Define a baseline for a metric.
    /// </summary>
    public void DefineBaseline(string metricName, PerformanceBaseline baseline)
    {
        _baselines[metricName] = baseline ?? throw new ArgumentNullException(nameof(baseline));
    }

    /// <summary>
    /// Check if current value meets baseline expectations.
    /// </summary>
    public PerformanceCheckResult CheckBaseline(string metricName, double currentValue)
    {
        if (!_baselines.TryGetValue(metricName, out var baseline))
        {
            return new PerformanceCheckResult { Status = BaselineStatus.Unknown };
        }

        var deviation = ((currentValue - baseline.Target) / baseline.Target) * 100;

        return new PerformanceCheckResult
        {
            MetricName = metricName,
            CurrentValue = currentValue,
            BaselineTarget = baseline.Target,
            Deviation = deviation,
            Status = DetermineStatus(deviation, baseline),
            Recommendation = GenerateRecommendation(metricName, deviation, baseline)
        };
    }

    /// <summary>
    /// Get all defined baselines.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceBaseline> GetAllBaselines()
        => _baselines.AsReadOnly();

    private static BaselineStatus DetermineStatus(double deviation, PerformanceBaseline baseline)
    {
        if (deviation <= baseline.WarningThreshold)
        {
            return BaselineStatus.Healthy;
        }

        if (deviation <= baseline.CriticalThreshold)
        {
            return BaselineStatus.Warning;
        }

        return BaselineStatus.Critical;
    }

    private static string GenerateRecommendation(string metricName, double deviation, PerformanceBaseline baseline)
    {
        if (deviation <= baseline.WarningThreshold)
        {
            return "Performance within acceptable range";
        }

        var part = metricName.ToLower() switch
        {
            var m when m.Contains("latency") || m.Contains("duration") =>
                "Consider: checking database indexes, reducing lock contention, or adding caching",
            var m when m.Contains("throughput") || m.Contains("count") =>
                "Consider: parallelizing operations, batching requests, or optimizing I/O",
            var m when m.Contains("memory") =>
                "Consider: implementing pagination, streaming, or reducing object retention",
            _ => "Investigate performance characteristics"
        };

        return $"{part}. Deviation: {deviation:+0.0;-0.0}%";
    }
}

/// <summary>
/// Represents a performance baseline definition.
/// </summary>
public class PerformanceBaseline
{
    /// <summary>
    /// Target expected value for the metric.
    /// </summary>
    public required double Target { get; set; }

    /// <summary>
    /// Deviation threshold (%) for warning state (default: 20%).
    /// </summary>
    public double WarningThreshold { get; set; } = 20.0;

    /// <summary>
    /// Deviation threshold (%) for critical state (default: 50%).
    /// </summary>
    public double CriticalThreshold { get; set; } = 50.0;

    /// <summary>
    /// Optional description of the baseline.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Result of a baseline check.
/// </summary>
public class PerformanceCheckResult
{
    /// <summary>Metric name being checked.</summary>
    public string? MetricName { get; set; }
    /// <summary>Current measured value.</summary>
    public double CurrentValue { get; set; }
    /// <summary>Target baseline value.</summary>
    public double BaselineTarget { get; set; }
    /// <summary>Percentage deviation from target.</summary>
    public double Deviation { get; set; }
    /// <summary>Status result of the check.</summary>
    public required BaselineStatus Status { get; set; }
    /// <summary>Recommendation for addressing deviations.</summary>
    public string Recommendation { get; set; } = "";
}

/// <summary>
/// Status of performance baseline check.
/// </summary>
public enum BaselineStatus
{
    /// <summary>Performance is within acceptable thresholds.</summary>
    Healthy,
    /// <summary>Performance deviation exceeds warning threshold.</summary>
    Warning,
    /// <summary>Performance deviation exceeds critical threshold.</summary>
    Critical,
    /// <summary>No baseline defined for this metric.</summary>
    Unknown
}
