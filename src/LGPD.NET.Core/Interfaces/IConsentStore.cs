using LGPD.NET.Core.Enums;
using LGPD.NET.Core.Models;

namespace LGPD.NET.Core.Interfaces;

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

    /// <summary>Revokes a consent record by data subject and purpose.</summary>
    /// <param name="dataSubjectId">Data subject identifier.</param>
    /// <param name="purpose">Processing purpose.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeAsync(string dataSubjectId, ProcessingPurpose purpose, CancellationToken cancellationToken = default);
}
