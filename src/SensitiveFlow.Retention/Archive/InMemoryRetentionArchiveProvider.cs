using System.Collections.Concurrent;

namespace SensitiveFlow.Retention.Archive;

/// <summary>
/// Thread-safe in-memory implementation of retention archive storage.
/// </summary>
public class InMemoryRetentionArchiveProvider : IRetentionArchiveProvider
{
    private readonly ConcurrentDictionary<string, List<object>> _archives = new();

    /// <summary>
    /// Archives a collection of entities under a given key.
    /// </summary>
    public Task ArchiveAsync(IEnumerable<object> entities, string archiveKey, CancellationToken cancellationToken = default)
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities));
        }

        if (string.IsNullOrEmpty(archiveKey))
        {
            throw new ArgumentNullException(nameof(archiveKey));
        }

        var entityList = entities.ToList();
        _archives[archiveKey] = entityList;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves all entities from a previously archived batch.
    /// </summary>
    public Task<IReadOnlyList<object>> RetrieveAsync(string archiveKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(archiveKey))
        {
            throw new ArgumentNullException(nameof(archiveKey));
        }

        if (_archives.TryGetValue(archiveKey, out var entities))
        {
            return Task.FromResult<IReadOnlyList<object>>(entities.AsReadOnly());
        }

        return Task.FromResult<IReadOnlyList<object>>(new List<object>().AsReadOnly());
    }

    /// <summary>
    /// Lists all archive keys that have been stored.
    /// </summary>
    public Task<IReadOnlyList<string>> ListArchiveKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = _archives.Keys.OrderBy(k => k).ToList().AsReadOnly();
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }
}
