using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Stream abstraction for querying large result sets without materializing all records in memory.
/// </summary>
public interface IAuditStreamQuery : IAsyncEnumerable<AuditRecord>
{
    /// <summary>
    /// Gets the total count of records matching the query criteria (before streaming).
    /// </summary>
    /// <remarks>
    /// This may execute a count query against the underlying store. Use sparingly on large datasets.
    /// </remarks>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a snapshot of the query criteria that generated this stream.
    /// </summary>
    AuditQuery QueryCriteria { get; }
}
