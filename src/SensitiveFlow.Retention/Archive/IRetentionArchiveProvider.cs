namespace SensitiveFlow.Retention.Archive;

/// <summary>
/// Abstraction for archiving and retrieving entities in cold storage.
/// </summary>
public interface IRetentionArchiveProvider
{
    /// <summary>
    /// Archives a collection of entities under a given key.
    /// </summary>
    /// <param name="entities">The entities to archive.</param>
    /// <param name="archiveKey">A unique identifier for this archive batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ArchiveAsync(IEnumerable<object> entities, string archiveKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities from a previously archived batch.
    /// </summary>
    /// <param name="archiveKey">The unique identifier for the archive batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The archived entities, or an empty list if not found.</returns>
    Task<IReadOnlyList<object>> RetrieveAsync(string archiveKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all archive keys that have been stored.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all archive keys in the provider.</returns>
    Task<IReadOnlyList<string>> ListArchiveKeysAsync(CancellationToken cancellationToken = default);
}
