using System.Collections.Concurrent;
using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Stores;

/// <summary>
/// In-memory implementation of <see cref="ITokenStore"/> for tests and single-session batch processing.
/// Mappings are lost when the process exits — do NOT use in production.
/// For production, implement <see cref="ITokenStore"/> backed by a durable store (SQL, Redis, etc.).
/// </summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, string> _valueToToken = new();
    private readonly ConcurrentDictionary<string, string> _tokenToValue = new();

    /// <inheritdoc />
    public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        var token = _valueToToken.GetOrAdd(value, v =>
        {
            var newToken = Guid.NewGuid().ToString();
            _tokenToValue[newToken] = v;
            return newToken;
        });

        return Task.FromResult(token);
    }

    /// <inheritdoc />
    public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (_tokenToValue.TryGetValue(token, out var value))
        {
            return Task.FromResult(value);
        }

        throw new KeyNotFoundException($"Token '{token}' was not found. The store may have been restarted or the token was never created in this instance.");
    }
}
