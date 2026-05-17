using System.Collections.Concurrent;
using System.Diagnostics;

namespace SensitiveFlow.HealthChecks.PerformanceMetrics;

/// <summary>
/// Collects performance metrics for health checks and audit operations.
/// </summary>
public sealed class HealthCheckPerformanceCollector
{
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics = new();

    /// <summary>
    /// Records a health check execution.
    /// </summary>
    public void RecordHealthCheck(string checkName, long elapsedMilliseconds, bool success)
    {
        ArgumentNullException.ThrowIfNull(checkName);

        _metrics.AddOrUpdate(
            checkName,
            new PerformanceMetric { CheckName = checkName, Count = 1, TotalTimeMs = elapsedMilliseconds, Failures = success ? 0 : 1 },
            (_, existing) =>
            {
                existing.Count++;
                existing.TotalTimeMs += elapsedMilliseconds;
                if (!success)
                {
                    existing.Failures++;
                }
                return existing;
            });
    }

    /// <summary>
    /// Records audit store operation metrics.
    /// </summary>
    public void RecordAuditOperation(string operationType, long recordCount, long elapsedMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(operationType);

        var key = $"Audit.{operationType}";
        _metrics.AddOrUpdate(
            key,
            new PerformanceMetric { CheckName = key, Count = 1, TotalTimeMs = elapsedMilliseconds, RecordCount = recordCount },
            (_, existing) =>
            {
                existing.Count++;
                existing.TotalTimeMs += elapsedMilliseconds;
                existing.RecordCount += recordCount;
                return existing;
            });
    }

    /// <summary>
    /// Gets metrics for a specific check.
    /// </summary>
    public PerformanceMetric? GetMetric(string checkName)
    {
        _metrics.TryGetValue(checkName, out var metric);
        return metric;
    }

    /// <summary>
    /// Gets all recorded metrics.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetric> GetAllMetrics() => _metrics;

    /// <summary>
    /// Gets average latency across all checks.
    /// </summary>
    public double GetAverageLatencyMs()
    {
        var metrics = _metrics.Values.ToList();
        return metrics.Count > 0 ? metrics.Sum(m => m.TotalTimeMs) / (double)metrics.Sum(m => m.Count) : 0;
    }

    /// <summary>
    /// Gets throughput (records per second) for audit operations.
    /// </summary>
    public double GetAuditThroughputRecordsPerSec()
    {
        var auditMetrics = _metrics.Values.Where(m => m.CheckName.StartsWith("Audit.")).ToList();
        if (auditMetrics.Count == 0 || auditMetrics.Sum(m => m.TotalTimeMs) == 0)
        {
            return 0;
        }

        var totalRecords = auditMetrics.Sum(m => m.RecordCount);
        var totalSeconds = auditMetrics.Sum(m => m.TotalTimeMs) / 1000.0;
        return totalRecords / totalSeconds;
    }

    /// <summary>
    /// Gets the success rate across all checks.
    /// </summary>
    public double GetSuccessRate()
    {
        var metrics = _metrics.Values.ToList();
        if (metrics.Count == 0 || metrics.Sum(m => m.Count) == 0)
        {
            return 100;
        }

        var totalChecks = metrics.Sum(m => m.Count);
        var totalFailures = metrics.Sum(m => m.Failures);
        return ((totalChecks - totalFailures) / (double)totalChecks) * 100;
    }

    /// <summary>
    /// Gets checks that exceed a latency threshold.
    /// </summary>
    public IEnumerable<PerformanceMetric> GetSlowChecks(int thresholdMs)
    {
        return _metrics.Values.Where(m => m.AverageTimeMs > thresholdMs).OrderByDescending(m => m.AverageTimeMs);
    }

    /// <summary>
    /// Clears all recorded metrics.
    /// </summary>
    public void Clear()
    {
        _metrics.Clear();
    }
}

/// <summary>
/// Performance metrics for a specific health check or operation.
/// </summary>
public sealed class PerformanceMetric
{
    /// <summary>Gets the check name.</summary>
    public string CheckName { get; set; } = string.Empty;

    /// <summary>Gets the number of executions.</summary>
    public long Count { get; set; }

    /// <summary>Gets the total time in milliseconds.</summary>
    public long TotalTimeMs { get; set; }

    /// <summary>Gets the number of failures.</summary>
    public long Failures { get; set; }

    /// <summary>Gets the total record count processed.</summary>
    public long RecordCount { get; set; }

    /// <summary>Gets the average time per execution.</summary>
    public double AverageTimeMs => Count > 0 ? (double)TotalTimeMs / Count : 0;

    /// <summary>Gets the minimum time (approximation based on average).</summary>
    public double MinTimeMs => Count > 1 ? AverageTimeMs * 0.8 : AverageTimeMs;

    /// <summary>Gets the maximum time (approximation based on average).</summary>
    public double MaxTimeMs => Count > 1 ? AverageTimeMs * 1.2 : AverageTimeMs;
}
