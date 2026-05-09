using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace SensitiveFlow.Audit.EFCore.ContainerTests;

[Trait("Category", "Container")]
public sealed class RedisTokenStoreContainerTests : IClassFixture<RedisFixture>, IAsyncLifetime
{
    private readonly RedisFixture _fixture;
    private ServiceProvider _provider = null!;

    public RedisTokenStoreContainerTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        var services = new ServiceCollection();
        var multiplexer = ConnectionMultiplexer.Connect(_fixture.GetConnectionString());
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<ITokenStore, RedisTokenStore>();

        _provider = services.BuildServiceProvider(validateScopes: true);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetOrCreateToken_CreatesAndReturnsSameToken()
    {
        var store = _provider.GetRequiredService<ITokenStore>();

        var token1 = await store.GetOrCreateTokenAsync("user@example.com");
        var token2 = await store.GetOrCreateTokenAsync("user@example.com");

        token1.Should().Be(token2);
        token1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResolveToken_ReturnsOriginalValue()
    {
        var store = _provider.GetRequiredService<ITokenStore>();

        var token = await store.GetOrCreateTokenAsync("192.168.1.1");
        var resolved = await store.ResolveTokenAsync(token);

        resolved.Should().Be("192.168.1.1");
    }

    [Fact]
    public async Task ResolveToken_UnknownToken_ThrowsKeyNotFound()
    {
        var store = _provider.GetRequiredService<ITokenStore>();

        var act = () => store.ResolveTokenAsync("nonexistent-token");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetOrCreateToken_DifferentValues_ProduceDifferentTokens()
    {
        var store = _provider.GetRequiredService<ITokenStore>();

        var token1 = await store.GetOrCreateTokenAsync("value-a");
        var token2 = await store.GetOrCreateTokenAsync("value-b");

        token1.Should().NotBe(token2);
    }

    private sealed class RedisTokenStore : ITokenStore
    {
        private readonly IDatabase _db;

        public RedisTokenStore(IConnectionMultiplexer multiplexer)
        {
            _db = multiplexer.GetDatabase();
        }

        public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
        {
            var tokenKey = $"token:v:{value}";
            var existing = await _db.StringGetAsync(tokenKey);
            if (existing.HasValue)
            {
                return existing.ToString();
            }

            var token = Guid.NewGuid().ToString("N");
            var valueKey = $"token:t:{token}";

            // Use a Lua script for atomicity: set both keys only if the value key doesn't exist yet.
            var script = LuaScript.Prepare(@"
                if redis.call('EXISTS', @tokenKey) == 1 then
                    return redis.call('GET', @tokenKey)
                end
                redis.call('SET', @tokenKey, @token)
                redis.call('SET', @valueKey, @value)
                return @token
            ");

            var result = await _db.ScriptEvaluateAsync(script, new
            {
                tokenKey = (RedisKey)tokenKey,
                valueKey = (RedisKey)valueKey,
                token = (RedisValue)token,
                value = (RedisValue)value,
            });

            return result.ToString();
        }

        public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var valueKey = $"token:t:{token}";
            var value = await _db.StringGetAsync(valueKey);

            if (!value.HasValue)
            {
                throw new KeyNotFoundException($"Token '{token}' not found in the store.");
            }

            return value.ToString();
        }
    }
}

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string GetConnectionString() => _container.GetConnectionString();
}
