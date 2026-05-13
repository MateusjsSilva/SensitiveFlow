using StackExchange.Redis;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.TokenStore.Redis;

/// <summary>
/// Distributed token store backed by Redis for high-throughput pseudonymization.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses Redis as a distributed cache for token mappings, enabling
/// horizontal scaling across multiple application instances. Token-to-value mappings are
/// stored with optional TTL for automatic expiration.
/// </para>
/// <para>
/// <b>Important:</b> Redis is not a durable store by default. For production use with
/// retention requirements, configure Redis persistence (RDB snapshots or AOF).
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// var redis = ConnectionMultiplexer.Connect("localhost:6379");
/// var tokenStore = new RedisTokenStore(redis, keyPrefix: "app:tokens:");
/// var token = await tokenStore.GetOrCreateTokenAsync("alice@example.com");
/// </code>
/// </para>
/// </remarks>
public sealed class RedisTokenStore : ITokenStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;
    private readonly string _reverseKeyPrefix;
    private readonly TimeSpan? _defaultExpiry;

    /// <summary>
    /// Initializes a new instance of <see cref="RedisTokenStore"/>.
    /// </summary>
    /// <param name="redis">Connected Redis client.</param>
    /// <param name="keyPrefix">Prefix for forward mappings (token → value). Defaults to "tokens:".</param>
    /// <param name="defaultExpiry">Optional TTL for keys. If <c>null</c>, keys never expire (careful with memory!).</param>
    public RedisTokenStore(
        IConnectionMultiplexer redis,
        string keyPrefix = "tokens:",
        TimeSpan? defaultExpiry = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _keyPrefix = keyPrefix ?? throw new ArgumentNullException(nameof(keyPrefix));
        _reverseKeyPrefix = keyPrefix + "rev:";
        _defaultExpiry = defaultExpiry;
    }

    /// <inheritdoc />
    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var db = _redis.GetDatabase();
        var reverseKey = _reverseKeyPrefix + value;

        // Check if token already exists
        var existingToken = await db.StringGetAsync(reverseKey);
        if (existingToken.HasValue && !existingToken.IsNull)
        {
            // Refresh TTL if configured
            if (_defaultExpiry.HasValue)
            {
                await db.KeyExpireAsync(reverseKey, _defaultExpiry);
            }

            return existingToken.ToString();
        }

        // Generate new token
        var token = GenerateToken();
        var tokenKey = _keyPrefix + token;

        // Store bidirectional mapping
        var transaction = db.CreateTransaction();
        _ = transaction.StringSetAsync(tokenKey, value, _defaultExpiry);
        _ = transaction.StringSetAsync(reverseKey, token, _defaultExpiry);

        var success = await transaction.ExecuteAsync();
        if (!success)
        {
            throw new InvalidOperationException(
                $"Failed to store token mapping in Redis. Token: {token}, Value: {value}");
        }

        return token;
    }

    /// <inheritdoc />
    public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var db = _redis.GetDatabase();
        var tokenKey = _keyPrefix + token;

        var value = await db.StringGetAsync(tokenKey);
        if (!value.HasValue || value.IsNull)
        {
            throw new KeyNotFoundException(
                $"Token '{token}' not found in Redis token store. " +
                "It may have expired or never existed.");
        }

        return value.ToString();
    }

    /// <summary>
    /// Deletes a token mapping from Redis (for manual cleanup or right-to-erasure).
    /// </summary>
    /// <param name="token">The token to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> DeleteTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var db = _redis.GetDatabase();
        var tokenKey = _keyPrefix + token;

        // First, resolve the value to delete the reverse mapping
        var value = await db.StringGetAsync(tokenKey);
        if (!value.HasValue || value.IsNull)
        {
            return false;
        }

        var reverseKey = _reverseKeyPrefix + value.ToString();

        // Delete both forward and reverse mappings
        var transaction = db.CreateTransaction();
        _ = transaction.KeyDeleteAsync(tokenKey);
        _ = transaction.KeyDeleteAsync(reverseKey);

        return await transaction.ExecuteAsync();
    }

    /// <summary>
    /// Retrieves health information about the Redis connection.
    /// </summary>
    /// <returns><c>true</c> if Redis is reachable and responding; <c>false</c> otherwise.</returns>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var pong = await db.ExecuteAsync("ping");
            return pong?.ToString()?.Equals("PONG", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the count of stored tokens in Redis (approximate for large datasets).
    /// </summary>
    public async Task<long> GetTokenCountAsync()
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = await server.KeysAsync(pattern: _keyPrefix + "*");
        return keys.Count();
    }

    private static string GenerateToken()
    {
        // Generate a URL-safe base64 token
        var bytes = new byte[16];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return "tok_" + token;
    }
}
