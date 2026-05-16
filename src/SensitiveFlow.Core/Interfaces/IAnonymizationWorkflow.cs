using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Helper service for anonymizing all audit records and related data for a deleted data subject.
/// Implements GDPR Article 17 / LGPD Article 16 (Right to Erasure / Deletion).
/// </summary>
public interface IAnonymizationWorkflow
{
    /// <summary>
    /// Anonymizes all audit records for a specific data subject.
    /// </summary>
    /// <param name="dataSubjectId">The data subject being deleted.</param>
    /// <param name="anonymizationToken">A durable, unique token (e.g., hash of dataSubjectId) to use as a replacement identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of audit records anonymized.</returns>
    /// <remarks>
    /// <para>
    /// After a successful data subject deletion:
    /// 1. Call this method to replace all references to <paramref name="dataSubjectId"/> with <paramref name="anonymizationToken"/>.
    /// 2. The audit trail remains intact (you can still prove what happened), but the subject is deanonymized.
    /// 3. Combined with data deletion, this fulfills GDPR Article 17 / LGPD Article 16.
    /// </para>
    /// <para>
    /// Best practice: Generate <paramref name="anonymizationToken"/> by hashing the original DataSubjectId with a time-bound salt,
    /// then store the mapping in a secured, separately-encrypted table for future verification without revealing the original ID.
    /// </para>
    /// </remarks>
    Task<int> AnonymizeByDataSubjectAsync(
        string dataSubjectId,
        string anonymizationToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymizes records matching a query pattern, such as all records from a deleted tenant.
    /// </summary>
    /// <param name="query">Query criteria (e.g., .ByEntity("TenantId").ByOperation("Delete")).</param>
    /// <param name="anonymizationToken">Token to use as replacement identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of records anonymized.</returns>
    Task<int> AnonymizeByQueryAsync(
        AuditQuery query,
        string anonymizationToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a data subject has been anonymized.
    /// </summary>
    /// <param name="dataSubjectId">The data subject to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the subject has been fully anonymized; false otherwise.</returns>
    Task<bool> IsAnonymizedAsync(
        string dataSubjectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the anonymization token for a deleted data subject (if available).
    /// </summary>
    /// <param name="dataSubjectId">The original data subject ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The anonymization token if found; null if not anonymized or not recorded.</returns>
    /// <remarks>
    /// This should only be called in secure contexts (e.g., internal audit verification).
    /// Return values should not be exposed to untrusted callers.
    /// </remarks>
    Task<string?> GetAnonymizationTokenAsync(
        string dataSubjectId,
        CancellationToken cancellationToken = default);
}
