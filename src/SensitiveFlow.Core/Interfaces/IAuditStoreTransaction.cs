namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Optional audit-store capability for executing related audit operations in the
/// same durable transaction.
/// </summary>
public interface IAuditStoreTransaction
{
    /// <summary>Executes the supplied operation in the store's transaction boundary.</summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
