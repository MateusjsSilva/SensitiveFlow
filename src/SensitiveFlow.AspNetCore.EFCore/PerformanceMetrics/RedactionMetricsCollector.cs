using System.Collections.Concurrent;
using System.Diagnostics;

namespace SensitiveFlow.AspNetCore.EFCore.PerformanceMetrics;

/// <summary>
/// Collects performance metrics about redaction operations.
/// </summary>
public sealed class RedactionMetricsCollector
{
    private readonly ConcurrentDictionary<string, RedactionMetric> _metrics = new();
    private long _totalOperations;
    private long _totalFieldsRedacted;
    private long _totalTimeMs;

    /// <summary>
    /// Records a redaction operation.
    /// </summary>
    public void RecordOperation(string fieldName, int fieldsAffected, long elapsedMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(fieldName);

        _metrics.AddOrUpdate(
            fieldName,
            new RedactionMetric { FieldName = fieldName, Count = 1, TotalTimeMs = elapsedMilliseconds },
            (_, existing) =>
            {
                existing.Count++;
                existing.TotalTimeMs += elapsedMilliseconds;
                return existing;
            });

        Interlocked.Increment(ref _totalOperations);
        Interlocked.Add(ref _totalFieldsRedacted, fieldsAffected);
        Interlocked.Add(ref _totalTimeMs, elapsedMilliseconds);
    }

    /// <summary>
    /// Gets metrics for a specific field.
    /// </summary>
    public RedactionMetric? GetMetric(string fieldName)
    {
        _metrics.TryGetValue(fieldName, out var metric);
        return metric;
    }

    /// <summary>
    /// Gets all recorded metrics.
    /// </summary>
    public IReadOnlyDictionary<string, RedactionMetric> GetAllMetrics() => _metrics;

    /// <summary>
    /// Gets the total number of redaction operations.
    /// </summary>
    public long TotalOperations => Interlocked.Read(ref _totalOperations);

    /// <summary>
    /// Gets the total number of fields redacted.
    /// </summary>
    public long TotalFieldsRedacted => Interlocked.Read(ref _totalFieldsRedacted);

    /// <summary>
    /// Gets the total time spent on redaction operations in milliseconds.
    /// </summary>
    public long TotalTimeMs => Interlocked.Read(ref _totalTimeMs);

    /// <summary>
    /// Gets the average time per operation in milliseconds.
    /// </summary>
    public double AverageTimeMs => TotalOperations > 0 ? (double)TotalTimeMs / TotalOperations : 0;

    /// <summary>
    /// Clears all recorded metrics.
    /// </summary>
    public void Clear()
    {
        _metrics.Clear();
        Interlocked.Exchange(ref _totalOperations, 0);
        Interlocked.Exchange(ref _totalFieldsRedacted, 0);
        Interlocked.Exchange(ref _totalTimeMs, 0);
    }

    /// <summary>
    /// Gets a snapshot of current metrics as a summary string.
    /// </summary>
    public string GetSummary()
    {
        return $"Operations: {TotalOperations}, Fields: {TotalFieldsRedacted}, AvgTime: {AverageTimeMs:F2}ms";
    }
}

/// <summary>
/// Represents metrics for a specific redacted field.
/// </summary>
public sealed class RedactionMetric
{
    /// <summary>Gets the field name.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Gets the number of times this field was redacted.</summary>
    public long Count { get; set; }

    /// <summary>Gets the total time spent redacting this field in milliseconds.</summary>
    public long TotalTimeMs { get; set; }

    /// <summary>Gets the average time per redaction for this field.</summary>
    public double AverageTimeMs => Count > 0 ? (double)TotalTimeMs / Count : 0;
}
