namespace SensitiveFlow.Retention.Analytics;

/// <summary>
/// Records execution metrics from a single retention run.
/// </summary>
/// <remarks>
/// Properties:
/// - RunAt: The time the run was executed
/// - AnonymizedCount: The number of fields anonymized during this run
/// - DeletePendingCount: The number of entities marked for deletion during this run
/// - DurationMs: The duration of the run in milliseconds
/// </remarks>
public sealed record RetentionRunRecord(
    DateTimeOffset RunAt,
    int AnonymizedCount,
    int DeletePendingCount,
    double DurationMs
);
