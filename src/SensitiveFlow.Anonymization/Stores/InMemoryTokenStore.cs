using System.Collections.Concurrent;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Stores;

/// <summary>
/// In-memory implementation of <see cref="ITokenStore"/> for tests and single-session batch processing.
/// Mappings are lost when the process exits — do NOT use in production.
/// For production, implement <see cref="ITokenStore"/> backed by a durable store (SQL, Redis, etc.).
/// </summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    // Single lock ensures both dictionaries stay in sync under concurrent access.
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _valueToToken = new();
    private readonly Dictionary<string, string> _tokenToValue = new();

    /// <inheritdoc />
    public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_valueToToken.TryGetValue(value, out var existing))
            {
                return Task.FromResult(existing);
            }

            var newToken = Guid.NewGuid().ToString();
            _valueToToken[value]    = newToken;
            _tokenToValue[newToken] = value;
            return Task.FromResult(newToken);
        }
    }

    /// <inheritdoc />
    public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_tokenToValue.TryGetValue(token, out var value))
            {
                return Task.FromResult(value);
            }
        }

        throw new KeyNotFoundException($"Token '{token}' was not found. The store may have been restarted or the token was never created in this instance.");
    }
}

