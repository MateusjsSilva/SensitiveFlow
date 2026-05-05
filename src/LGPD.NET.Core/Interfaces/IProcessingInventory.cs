using LGPD.NET.Core.Models;

namespace LGPD.NET.Core.Interfaces;

/// <summary>
/// Store for processing operation records under Art. 37 of the LGPD.
/// </summary>
public interface IProcessingInventory
{
    Task SaveAsync(ProcessingOperationRecord record, CancellationToken cancellationToken = default);
    Task<ProcessingOperationRecord?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessingOperationRecord>> ListAsync(CancellationToken cancellationToken = default);
}
