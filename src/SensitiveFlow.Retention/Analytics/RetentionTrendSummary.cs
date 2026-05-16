namespace SensitiveFlow.Retention.Analytics;

/// <summary>
/// Aggregated summary of retention execution trends.
/// </summary>
/// <remarks>
/// Properties:
/// - TotalRuns: The total number of retention runs recorded
/// - TotalAnonymized: The total number of fields anonymized across all runs
/// - TotalDeletePending: The total number of entities marked for deletion across all runs
/// - AverageAnonymizedPerRun: The average number of fields anonymized per run
/// - LastRunAt: The time of the most recent run, or null if no runs recorded
/// - PeakAnonymizedRun: The run with the highest anonymized count, or null if no runs recorded
/// </remarks>
public sealed record RetentionTrendSummary(
    int TotalRuns,
    int TotalAnonymized,
    int TotalDeletePending,
    double AverageAnonymizedPerRun,
    DateTimeOffset? LastRunAt,
    RetentionRunRecord? PeakAnonymizedRun
);
