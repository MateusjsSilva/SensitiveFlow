using System.Collections.Concurrent;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Analytics;

/// <summary>
/// Thread-safe in-memory collector of retention execution analytics.
/// </summary>
public class RetentionAnalyticsCollector : IRetentionAnalyticsCollector
{
    private readonly ConcurrentBag<RetentionRunRecord> _records = new();

    /// <summary>
    /// Records the result of a retention execution run.
    /// </summary>
    public void RecordRun(RetentionExecutionReport report, DateTimeOffset ranAt, double durationMs)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        var record = new RetentionRunRecord(
            ranAt,
            report.AnonymizedFieldCount,
            report.DeletePendingEntityCount,
            durationMs
        );

        _records.Add(record);
    }

    /// <summary>
    /// Gets the complete run history.
    /// </summary>
    public IReadOnlyList<RetentionRunRecord> GetRunHistory()
    {
        return _records.OrderBy(r => r.RunAt).ToList();
    }

    /// <summary>
    /// Gets aggregated trend summary across all runs.
    /// </summary>
    public RetentionTrendSummary GetTrendSummary()
    {
        var history = GetRunHistory();

        if (history.Count == 0)
        {
            return new RetentionTrendSummary(
                TotalRuns: 0,
                TotalAnonymized: 0,
                TotalDeletePending: 0,
                AverageAnonymizedPerRun: 0,
                LastRunAt: null,
                PeakAnonymizedRun: null
            );
        }

        var totalAnonymized = history.Sum(r => r.AnonymizedCount);
        var totalDeletePending = history.Sum(r => r.DeletePendingCount);
        var averageAnonymized = (double)totalAnonymized / history.Count;
        var lastRun = history.Last();
        var peakRun = history.OrderByDescending(r => r.AnonymizedCount).First();

        return new RetentionTrendSummary(
            TotalRuns: history.Count,
            TotalAnonymized: totalAnonymized,
            TotalDeletePending: totalDeletePending,
            AverageAnonymizedPerRun: averageAnonymized,
            LastRunAt: lastRun.RunAt,
            PeakAnonymizedRun: peakRun
        );
    }
}
