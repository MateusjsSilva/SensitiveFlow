using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SensitiveFlow.TestKit.Threading;

/// <summary>
/// Thread-safe variant of in-memory audit store using ConcurrentBag.
/// </summary>
public sealed class ThreadSafeAuditStore : IAuditStore
{
    private readonly ConcurrentBag<object> _records = new();

    /// <summary>
    /// Gets all stored records (snapshot).
    /// </summary>
    public IReadOnlyList<object> GetAllRecords() => _records.ToList().AsReadOnly();

    /// <summary>
    /// Adds a record thread-safely.
    /// </summary>
    public void AddRecord(object record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records.Add(record);
    }

    /// <summary>
    /// Gets record count.
    /// </summary>
    public int RecordCount => _records.Count;

    /// <summary>
    /// Clears all records.
    /// </summary>
    public void Clear()
    {
        while (_records.TryTake(out _))
        {
            // Drain bag
        }
    }

    /// <summary>
    /// Gets records of a specific type.
    /// </summary>
    public IEnumerable<T> GetRecordsOfType<T>()
    {
        return _records.OfType<T>();
    }
}

/// <summary>
/// Minimal audit store interface for testing.
/// </summary>
public interface IAuditStore
{
    /// <summary>Gets all records.</summary>
    IReadOnlyList<object> GetAllRecords();

    /// <summary>Adds a record.</summary>
    void AddRecord(object record);

    /// <summary>Gets record count.</summary>
    int RecordCount { get; }

    /// <summary>Clears all records.</summary>
    void Clear();
}
