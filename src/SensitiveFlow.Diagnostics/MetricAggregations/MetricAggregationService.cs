using System.Collections.Concurrent;

namespace SensitiveFlow.Diagnostics.MetricAggregations;

/// <summary>
/// Aggregates metrics into percentiles, quantiles, and histograms.
/// </summary>
public sealed class MetricAggregationService
{
    private readonly ConcurrentDictionary<string, List<double>> _measurements = new();

    /// <summary>
    /// Record a measurement for a given metric.
    /// </summary>
    public void Record(string metricName, double value)
    {
        _measurements
            .AddOrUpdate(
                metricName,
                new List<double> { value },
                (_, list) =>
                {
                    list.Add(value);
                    return list;
                });
    }

    /// <summary>
    /// Get the 50th percentile (median) for a metric.
    /// </summary>
    public double GetPercentile(string metricName, double percentile)
    {
        if (!_measurements.TryGetValue(metricName, out var values) || values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling((percentile / 100.0) * sorted.Count) - 1;
        return sorted[Math.Max(0, index)];
    }

    /// <summary>
    /// Get average for a metric.
    /// </summary>
    public double GetAverage(string metricName)
    {
        if (!_measurements.TryGetValue(metricName, out var values) || values.Count == 0)
        {
            return 0;
        }

        return values.Average();
    }

    /// <summary>
    /// Get min/max/count statistics.
    /// </summary>
    public (double Min, double Max, int Count, double Mean) GetStatistics(string metricName)
    {
        if (!_measurements.TryGetValue(metricName, out var values) || values.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        return (
            values.Min(),
            values.Max(),
            values.Count,
            values.Average());
    }

    /// <summary>
    /// Clear all measurements.
    /// </summary>
    public void Clear()
    {
        _measurements.Clear();
    }

    /// <summary>
    /// Clear measurements for a specific metric.
    /// </summary>
    public void Clear(string metricName)
    {
        _measurements.TryRemove(metricName, out _);
    }
}
