using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Store abstraction for immutable <see cref="AuditSnapshot"/> entries.
/// Kept separate from <see cref="IAuditStore"/> because the storage shape and query patterns
/// are different (snapshots can be large; queries are usually by aggregate identity).
/// </summary>
public interface IAuditSnapshotStore
{
    /// <summary>Appends a snapshot to the store.</summary>
    Task AppendAsync(AuditSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns snapshots for a single aggregate (e.g. all changes to Customer #42), ordered
    /// by <see cref="AuditSnapshot.Timestamp"/> ascending.
    /// </summary>
    Task<IReadOnlyList<AuditSnapshot>> QueryByAggregateAsync(
        string aggregate,
        string aggregateId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every snapshot belonging to a data subject. Useful for portability and erasure flows.
    /// </summary>
    Task<IReadOnlyList<AuditSnapshot>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);
}
