using SensitiveFlow.Core.Interfaces;
using StackExchange.Redis;

namespace Redis.Sample;

/// <summary>
/// Distributed <see cref="ITokenStore"/> backed by Redis. Use this in multi-instance deployments
/// where in-process or per-pod token caches would diverge — every instance must see the same
/// token-to-value mapping for pseudonymization to be reversible.
/// </summary>
/// <remarks>
/// <para>
/// Key layout: two keys per mapping kept in sync.
/// <list type="bullet">
///   <item><c>sf:tok:v2t:&lt;value-hash&gt;</c> → token (lookup-by-value, used by GetOrCreate).</item>
///   <item><c>sf:tok:t2v:&lt;token&gt;</c> → original value (lookup-by-token, used by Resolve).</item>
/// </list>
/// </para>
/// <para>
/// Concurrency: a Lua script performs the value→token mapping atomically. Two parallel callers
/// for the same value get the same token without producing duplicate mappings, which is the
/// failure mode the EF Core sample handles via <c>DbUpdateException</c> recovery.
/// </para>
/// <para>
/// <b>Hash the value</b> before using it as the v2t key. The value itself is sensitive (that is
/// the whole reason we're tokenizing it); do not store it in a key that ends up in Redis SLOWLOG
/// or AOF dumps verbatim. The hash is for key shaping only — the original value is still stored
/// under <c>sf:tok:t2v:&lt;token&gt;</c> so <see cref="ResolveTokenAsync"/> can return it.
/// </para>
/// </remarks>
public sealed class RedisTokenStore : ITokenStore
{
    private const string ValueToTokenPrefix = "sf:tok:v2t:";
    private const string TokenToValuePrefix = "sf:tok:t2v:";

    private static readonly LuaScript GetOrCreateScript = LuaScript.Prepare(@"
        local existing = redis.call('GET', @v2tKey)
        if existing then
            return existing
        end
        redis.call('SET', @v2tKey, @newToken)
        redis.call('SET', @t2vKey, @value)
        return @newToken
    ");

    private readonly IDatabase _db;

    /// <summary>Initializes a new instance.</summary>
    public RedisTokenStore(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _db = connection.GetDatabase();
    }

    /// <inheritdoc />
    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        cancellationToken.ThrowIfCancellationRequested();

        var v2tKey = ValueToTokenPrefix + HashValue(value);
        var newToken = Guid.NewGuid().ToString("N");
        var t2vKey = TokenToValuePrefix + newToken;

        var result = await _db.ScriptEvaluateAsync(GetOrCreateScript, new
        {
            v2tKey = (RedisKey)v2tKey,
            t2vKey = (RedisKey)t2vKey,
            newToken = (RedisValue)newToken,
            value = (RedisValue)value,
        }).ConfigureAwait(false);

        return ((RedisValue)result).ToString();
    }

    /// <inheritdoc />
    public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        cancellationToken.ThrowIfCancellationRequested();

        var value = await _db.StringGetAsync(TokenToValuePrefix + token).ConfigureAwait(false);
        if (!value.HasValue)
        {
            throw new KeyNotFoundException($"Token '{token}' not found in the store.");
        }
        return value.ToString();
    }

    private static string HashValue(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var digest = System.Security.Cryptography.SHA256.HashData(bytes);
#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(digest);
#else
        return Convert.ToHexString(digest).ToLowerInvariant();
#endif
    }
}
