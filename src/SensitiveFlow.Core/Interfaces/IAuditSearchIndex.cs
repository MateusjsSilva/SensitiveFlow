using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Full-text search index for audit records.
/// </summary>
/// <remarks>
/// <para>
/// Implementations can use various backends (Elasticsearch, Lucene, database full-text search, etc.)
/// to enable rich querying of audit trails by actor, IP, entity name, or other details.
/// </para>
/// </remarks>
public interface IAuditSearchIndex
{
    /// <summary>
    /// Indexes an audit record for full-text search.
    /// </summary>
    /// <param name="record">Audit record to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk indexes multiple audit records.
    /// </summary>
    /// <param name="records">Audit records to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexRangeAsync(
        IAsyncEnumerable<AuditRecord> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches audit records by actor/user.
    /// </summary>
    /// <param name="actorQuery">Partial or full actor name/ID to search for.</param>
    /// <param name="take">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records.</returns>
    Task<IReadOnlyList<AuditRecord>> SearchByActorAsync(
        string actorQuery,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches audit records by IP address token.
    /// </summary>
    /// <param name="ipQuery">IP token or partial match to search for.</param>
    /// <param name="take">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records.</returns>
    Task<IReadOnlyList<AuditRecord>> SearchByIpAsync(
        string ipQuery,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches audit records by entity name.
    /// </summary>
    /// <param name="entityQuery">Entity name or partial match to search for.</param>
    /// <param name="take">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records.</returns>
    Task<IReadOnlyList<AuditRecord>> SearchByEntityAsync(
        string entityQuery,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Free-form full-text search across all indexed fields.
    /// </summary>
    /// <param name="query">Free-text query (e.g., "DELETE User from 192.168").</param>
    /// <param name="take">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records ranked by relevance.</returns>
    Task<IReadOnlyList<AuditRecord>> SearchAsync(
        string query,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all index entries for a specific data subject (after anonymization or deletion).
    /// </summary>
    /// <param name="dataSubjectId">Data subject to remove from index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveByDataSubjectAsync(
        string dataSubjectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the entire index.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
