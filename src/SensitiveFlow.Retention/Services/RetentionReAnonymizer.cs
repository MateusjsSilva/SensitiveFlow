namespace SensitiveFlow.Retention.Services;

/// <summary>
/// Allows selective re-anonymization of entities without waiting for retention expiration.
/// </summary>
public class RetentionReAnonymizer
{
    /// <summary>
    /// Re-anonymizes entities matching a given predicate.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to potentially re-anonymize.</param>
    /// <param name="predicate">Filter to select which entities should be re-anonymized.</param>
    /// <param name="options">Executor options, or null for defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An execution report with re-anonymization results.</returns>
    public async Task<RetentionExecutionReport> ReAnonymizeAsync<T>(
        IEnumerable<T> entities,
        Func<T, bool> predicate,
        RetentionExecutorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities));
        }

        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        var executor = new RetentionExecutor(options ?? new());

        // Filter entities and cast to objects
        var filtered = entities.Where(predicate).Cast<object>().ToList();

        if (filtered.Count == 0)
        {
            return new RetentionExecutionReport();
        }

        // Use current time as reference (entities have already expired conceptually)
        var referenceDate = DateTimeOffset.UtcNow;
        Func<object, DateTimeOffset> referenceSelector = _ => referenceDate;

        return await executor.ExecuteAsync(filtered, referenceSelector, cancellationToken);
    }
}
