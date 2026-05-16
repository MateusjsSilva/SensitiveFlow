using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Store abstraction for immutable audit records.
/// </summary>
public interface IAuditStore
{
    /// <summary>Appends an audit record to the store.</summary>
    /// <param name="record">Audit record to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>Queries audit records by optional time range with pagination.</summary>
    /// <param name="from">Inclusive start timestamp.</param>
    /// <param name="to">Inclusive end timestamp.</param>
    /// <param name="skip">Number of records to skip (for pagination). Defaults to <c>0</c>.</param>
    /// <param name="take">Maximum number of records to return. Defaults to <c>100</c>. Use a value appropriate to your memory budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records, up to <paramref name="take"/> entries.</returns>
    Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Queries audit records for a specific data subject with pagination.</summary>
    /// <param name="dataSubjectId">Data subject identifier.</param>
    /// <param name="from">Inclusive start timestamp.</param>
    /// <param name="to">Inclusive end timestamp.</param>
    /// <param name="skip">Number of records to skip (for pagination). Defaults to <c>0</c>.</param>
    /// <param name="take">Maximum number of records to return. Defaults to <c>100</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records, up to <paramref name="take"/> entries.</returns>
    Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Queries audit records using a structured query builder.</summary>
    /// <param name="query">Query builder with filters, pagination, and ordering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records, up to the <c>Take</c> limit in the query.</returns>
    /// <remarks>
    /// This method enables efficient filtering on Entity, Operation, Actor, DataSubjectId, and Field
    /// without fetching all records into memory.
    /// Example:
    /// <code>
    /// var results = await store.QueryAsync(
    ///     new AuditQuery()
    ///         .ByEntity("User")
    ///         .ByOperation("Delete")
    ///         .InTimeRange(startDate, endDate)
    ///         .WithPagination(0, 50),
    ///     cancellationToken);
    /// </code>
    /// </remarks>
    Task<IReadOnlyList<AuditRecord>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);

    /// <summary>Streams audit records without materializing all results in memory.</summary>
    /// <param name="query">Query builder with filters and ordering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable stream of matching records. Pagination is ignored; stream the entire result set.</returns>
    /// <remarks>
    /// <para>
    /// Use this method to process large result sets (>10K records) without allocating memory for all records at once.
    /// Useful for bulk exports, archival, or deep analysis.
    /// </para>
    /// <para>
    /// Default implementation queries all records via <see cref="QueryAsync(AuditQuery, CancellationToken)"/> and enumerates them.
    /// Override for efficient streaming in database implementations.
    /// </para>
    /// <example>
    /// <code>
    /// var stream = store.QueryStreamAsync(
    ///     new AuditQuery()
    ///         .ByDataSubject("subject-123")
    ///         .InTimeRange(startDate, endDate));
    ///
    /// await foreach (var record in stream)
    /// {
    ///     await csv.WriteLineAsync(record);
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    async IAsyncEnumerable<AuditRecord> QueryStreamAsync(
        AuditQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Default implementation: query and enumerate
        var records = await QueryAsync(query, cancellationToken);
        foreach (var record in records)
        {
            yield return record;
        }
    }
}
