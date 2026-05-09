using System.Collections.Concurrent;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Decorators;

/// <summary>
/// Caches token mappings in front of a durable <see cref="ITokenStore"/> to reduce repeated store roundtrips.
/// </summary>
/// <remarks>
/// The cache is in-process and bounded by an approximate entry count. It stores original values in memory,
/// so applications with stricter memory exposure requirements should use a distributed or encrypted cache
/// in a custom decorator instead.
/// </remarks>
public sealed class CachingTokenStore : ITokenStore
{
    private readonly ITokenStore _inner;
    private readonly CachingTokenStoreOptions _options;
    private readonly ConcurrentDictionary<string, string> _valueToToken = new();
    private readonly ConcurrentDictionary<string, string> _tokenToValue = new();
    private readonly ConcurrentQueue<string> _entryOrder = new();

    /// <summary>Initializes a new instance of <see cref="CachingTokenStore"/>.</summary>
    public CachingTokenStore(ITokenStore inner, CachingTokenStoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
        _options = options ?? new CachingTokenStoreOptions();

        if (_options.MaxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum cache entries must be greater than zero.");
        }
    }

    /// <inheritdoc />
    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_valueToToken.TryGetValue(value, out var cachedToken))
        {
            return cachedToken;
        }

        var token = await _inner.GetOrCreateTokenAsync(value, cancellationToken).ConfigureAwait(false);
        Remember(value, token);
        return token;
    }

    /// <inheritdoc />
    public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (_tokenToValue.TryGetValue(token, out var cachedValue))
        {
            return cachedValue;
        }

        var value = await _inner.ResolveTokenAsync(token, cancellationToken).ConfigureAwait(false);
        Remember(value, token);
        return value;
    }

    private void Remember(string value, string token)
    {
        _valueToToken[value] = token;
        _tokenToValue[token] = value;
        _entryOrder.Enqueue(value);

        while (_valueToToken.Count > _options.MaxEntries && _entryOrder.TryDequeue(out var oldValue))
        {
            if (_valueToToken.TryRemove(oldValue, out var oldToken))
            {
                _tokenToValue.TryRemove(oldToken, out _);
            }
        }
    }
}

/// <summary>Options controlling <see cref="CachingTokenStore"/> cache size.</summary>
public sealed class CachingTokenStoreOptions
{
    /// <summary>Approximate maximum number of token mappings kept in memory. Default <c>1024</c>.</summary>
    public int MaxEntries { get; set; } = 1024;
}
