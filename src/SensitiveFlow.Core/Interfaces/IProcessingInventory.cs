using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Store for processing operation records under applicable privacy regulations.
/// </summary>
public interface IProcessingInventory
{
    /// <summary>Saves a processing operation record.</summary>
    /// <param name="record">Processing operation record to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(ProcessingOperationRecord record, CancellationToken cancellationToken = default);

    /// <summary>Gets a processing operation record by identifier.</summary>
    /// <param name="id">Processing operation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching processing operation record, or <see langword="null" />.</returns>
    Task<ProcessingOperationRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Lists all processing operation records.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing operation records.</returns>
    Task<IReadOnlyList<ProcessingOperationRecord>> ListAsync(CancellationToken cancellationToken = default);
}

