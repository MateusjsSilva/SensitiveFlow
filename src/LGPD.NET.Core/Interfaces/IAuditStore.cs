using LGPD.NET.Core.Models;

namespace LGPD.NET.Core.Interfaces;

/// <summary>
/// Store abstraction for immutable audit records.
/// </summary>
public interface IAuditStore
{
    /// <summary>Appends an audit record to the store.</summary>
    /// <param name="record">Audit record to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>Queries audit records by optional time range.</summary>
    /// <param name="from">Inclusive start timestamp.</param>
    /// <param name="to">Inclusive end timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records.</returns>
    Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>Queries audit records for a specific data subject.</summary>
    /// <param name="dataSubjectId">Data subject identifier.</param>
    /// <param name="from">Inclusive start timestamp.</param>
    /// <param name="to">Inclusive end timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching audit records.</returns>
    Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}
