using LGPD.NET.Core.Enums;
using LGPD.NET.Core.Models;

namespace LGPD.NET.Core.Interfaces;

public interface IConsentStore
{
    Task SaveAsync(ConsentRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConsentRecord>> ListByDataSubjectAsync(string dataSubjectId, CancellationToken cancellationToken = default);
    Task<ConsentRecord?> GetAsync(string dataSubjectId, ProcessingPurpose purpose, CancellationToken cancellationToken = default);
    Task RevokeAsync(string dataSubjectId, ProcessingPurpose purpose, CancellationToken cancellationToken = default);
}
