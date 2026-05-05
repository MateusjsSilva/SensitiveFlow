using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Store abstraction for consent records.
/// </summary>
public interface IConsentStore
{
    /// <summary>Saves a consent record.</summary>
    /// <param name="record">Consent record to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default);

    /// <summary>Lists consent records for a data subject.</summary>
    /// <param name="dataSubjectId">Data subject identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consent records for the data subject.</returns>
    Task<IReadOnlyList<ConsentRecord>> ListByDataSubjectAsync(string dataSubjectId, CancellationToken cancellationToken = default);

    /// <summary>Gets a consent record by data subject and purpose.</summary>
    /// <param name="dataSubjectId">Data subject identifier.</param>
    /// <param name="purpose">Processing purpose.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching consent record, or <see langword="null" />.</returns>
    Task<ConsentRecord?> GetAsync(string dataSubjectId, ProcessingPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the active consent for a data subject and purpose (Art. 8, §5 of the LGPD).
    /// </summary>
    /// <param name="dataSubjectId">Data subject identifier.</param>
    /// <param name="purpose">Processing purpose.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when an active consent record was found and revoked;
    /// <see langword="false"/> when no matching active record existed.
    /// Callers must treat <see langword="false"/> as a compliance event — a revocation
    /// request that cannot be confirmed may require manual follow-up.
    /// </returns>
    Task<bool> RevokeAsync(string dataSubjectId, ProcessingPurpose purpose, CancellationToken cancellationToken = default);
}
