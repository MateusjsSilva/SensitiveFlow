using LGPD.NET.Core.Enums;
using LGPD.NET.Core.Models;

namespace LGPD.NET.Core.Interfaces;

public interface IIncidentStore
{
    Task SaveAsync(IncidentRecord record, CancellationToken cancellationToken = default);
    Task<IncidentRecord?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentRecord>> QueryAsync(
        IncidentStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}
