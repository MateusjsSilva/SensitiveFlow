using System.Collections.Concurrent;

namespace SensitiveFlow.TokenStore.EFCore.Audit;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ITokenAuditSink"/>.
/// Stores audit records in a <see cref="ConcurrentQueue{T}"/> for inspection during testing or development.
/// </summary>
public sealed class InMemoryTokenAuditSink : ITokenAuditSink
{
    private readonly ConcurrentQueue<TokenAuditRecord> _records = new();

    /// <summary>
    /// Records a token operation by enqueuing it.
    /// </summary>
    /// <param name="record">The audit record to store.</param>
    /// <param name="cancellationToken">Unused; provided for interface compatibility.</param>
    /// <returns>A completed task.</returns>
    public Task RecordAsync(TokenAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records.Enqueue(record);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all recorded audit records in order of insertion.
    /// </summary>
    /// <returns>A read-only list of records.</returns>
    public IReadOnlyList<TokenAuditRecord> GetRecords()
    {
        return _records.ToList().AsReadOnly();
    }

    /// <summary>
    /// Clears all stored records. Useful for resetting state between tests.
    /// </summary>
    public void Clear()
    {
        while (_records.TryDequeue(out _))
        {
        }
    }
}
