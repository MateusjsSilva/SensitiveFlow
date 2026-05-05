namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Persistent store for pseudonymization token mappings.
/// Implementations must be durable — losing mappings makes pseudonymized data irrecoverable.
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// Returns the token associated with <paramref name="value"/>, creating and persisting
    /// a new one if it does not exist yet.
    /// </summary>
    /// <param name="value">Original value to pseudonymize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stable token for the given value.</returns>
    Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the original value for the given <paramref name="token"/>.
    /// </summary>
    /// <param name="token">Token previously returned by <see cref="GetOrCreateTokenAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The original value.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the token is not found.</exception>
    Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default);
}
