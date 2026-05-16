namespace SensitiveFlow.TokenStore.EFCore.Audit;

/// <summary>
/// Defines a recipient for token audit records.
/// Implementations can persist records to logs, databases, message queues, or other stores.
/// </summary>
public interface ITokenAuditSink
{
    /// <summary>
    /// Records a token operation asynchronously.
    /// </summary>
    /// <param name="record">The audit record to record.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RecordAsync(TokenAuditRecord record, CancellationToken cancellationToken = default);
}
