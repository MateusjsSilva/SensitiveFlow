using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Scheduling;

/// <summary>
/// Executes retention policies in parallel across multiple entity batches.
/// </summary>
public class ParallelRetentionExecutor
{
    /// <summary>
    /// Executes retention processing on multiple batches concurrently.
    /// </summary>
    /// <param name="batches">The batches of entities to process in parallel.</param>
    /// <param name="options">Executor options, or null for defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A merged report combining results from all batches.</returns>
    public async Task<RetentionExecutionReport> ExecuteParallelAsync(
        IEnumerable<RetentionBatch> batches,
        RetentionExecutorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (batches == null)
        {
            throw new ArgumentNullException(nameof(batches));
        }

        var executor = new RetentionExecutor(options ?? new());
        var batchList = batches.ToList();

        if (batchList.Count == 0)
        {
            return new RetentionExecutionReport();
        }

        var tasks = batchList.Select(batch =>
            executor.ExecuteAsync(batch.Entities, batch.ReferenceSelector, cancellationToken)
        ).ToList();

        var reports = await Task.WhenAll(tasks);

        return MergeReports(reports);
    }

    /// <summary>
    /// Merges multiple retention execution reports into a single report.
    /// </summary>
    private static RetentionExecutionReport MergeReports(IEnumerable<RetentionExecutionReport> reports)
    {
        var result = new RetentionExecutionReport();

        foreach (var report in reports)
        {
            foreach (var entry in report.Entries)
            {
                result.Add(entry);
            }
        }

        return result;
    }
}
