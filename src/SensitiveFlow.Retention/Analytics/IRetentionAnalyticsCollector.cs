using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Analytics;

/// <summary>
/// Collects and aggregates retention execution analytics.
/// </summary>
public interface IRetentionAnalyticsCollector
{
    /// <summary>
    /// Records the result of a retention execution run.
    /// </summary>
    /// <param name="report">The execution report from the run.</param>
    /// <param name="ranAt">The time the run occurred.</param>
    /// <param name="durationMs">The duration of the run in milliseconds.</param>
    void RecordRun(RetentionExecutionReport report, DateTimeOffset ranAt, double durationMs);

    /// <summary>
    /// Gets the complete run history.
    /// </summary>
    /// <returns>All recorded retention runs in chronological order.</returns>
    IReadOnlyList<RetentionRunRecord> GetRunHistory();

    /// <summary>
    /// Gets aggregated trend summary across all runs.
    /// </summary>
    /// <returns>A summary of retention trends and statistics.</returns>
    RetentionTrendSummary GetTrendSummary();
}
