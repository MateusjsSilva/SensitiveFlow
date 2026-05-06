using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Optional audit-store capability for appending multiple immutable audit records in one operation.
/// </summary>
public interface IBatchAuditStore : IAuditStore
{
    /// <summary>Appends audit records as a single logical operation.</summary>
    /// <param name="records">Audit records to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendRangeAsync(
        IReadOnlyCollection<AuditRecord> records,
        CancellationToken cancellationToken = default);
}
