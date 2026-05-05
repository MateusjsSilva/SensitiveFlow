using LGPD.NET.Core.Enums;
using LGPD.NET.Core.Models;

namespace LGPD.NET.Core.Interfaces;

/// <summary>
/// Store abstraction for security incident records.
/// </summary>
public interface IIncidentStore
{
    /// <summary>Saves an incident record.</summary>
    /// <param name="record">Incident record to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(IncidentRecord record, CancellationToken cancellationToken = default);

    /// <summary>Gets an incident record by identifier.</summary>
    /// <param name="id">Incident identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching incident record, or <see langword="null" />.</returns>
    Task<IncidentRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Queries incident records by status and optional time range.</summary>
    /// <param name="status">Optional incident status.</param>
    /// <param name="from">Inclusive start timestamp.</param>
    /// <param name="to">Inclusive end timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching incident records.</returns>
    Task<IReadOnlyList<IncidentRecord>> QueryAsync(
        IncidentStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}
