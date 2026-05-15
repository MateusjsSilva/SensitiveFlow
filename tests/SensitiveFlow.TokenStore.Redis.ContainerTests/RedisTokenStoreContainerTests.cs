using FluentAssertions;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.TokenStore.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace SensitiveFlow.TokenStore.Redis.ContainerTests;

[Trait("Category", "Container")]
public sealed class RedisTokenStoreContainerTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture;

    public RedisTokenStoreContainerTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IConnectionMultiplexer> GetConnectionAsync()
    {
        return await ConnectionMultiplexer.ConnectAsync(_fixture.GetConnectionString());
    }

    [Fact]
    public async Task GetOrCreateTokenAsync_ReturnsNewToken()
    {
        var connection = await GetConnectionAsync();
        var tokenStore = new RedisTokenStore(connection);
        var value = "test@example.com";

        var token = await tokenStore.GetOrCreateTokenAsync(value);

        token.Should().NotBeNullOrEmpty();
        token.Should().StartWith("tok_");
    }

    [Fact]
    public async Task GetOrCreateTokenAsync_ReturnsSameTokenForSameValue()
    {
        var connection = await GetConnectionAsync();
        var tokenStore = new RedisTokenStore(connection);
        var value = "alice@example.com";

        var token1 = await tokenStore.GetOrCreateTokenAsync(value);
        var token2 = await tokenStore.GetOrCreateTokenAsync(value);

        token1.Should().Be(token2);
    }

    [Fact]
    public async Task ResolveTokenAsync_ReturnsOriginalValue()
    {
        var connection = await GetConnectionAsync();
        var tokenStore = new RedisTokenStore(connection);
        var originalValue = "sensitive@data.com";

        var token = await tokenStore.GetOrCreateTokenAsync(originalValue);
        var resolved = await tokenStore.ResolveTokenAsync(token);

        resolved.Should().Be(originalValue);
    }

    [Fact]
    public async Task MultipleInstances_ShareTokens()
    {
        var connection = await GetConnectionAsync();
        var tokenStore1 = new RedisTokenStore(connection, keyPrefix: "app1:");
        var tokenStore2 = new RedisTokenStore(connection, keyPrefix: "app1:");
        var value = "shared-value@test.com";

        var token1 = await tokenStore1.GetOrCreateTokenAsync(value);
        var token2 = await tokenStore2.GetOrCreateTokenAsync(value);

        token1.Should().Be(token2);

        var resolved1 = await tokenStore1.ResolveTokenAsync(token1);
        var resolved2 = await tokenStore2.ResolveTokenAsync(token2);

        resolved1.Should().Be(resolved2).And.Be(value);
    }

    [Fact]
    public async Task PseudonymizeAndReverse_WithRedisBackend()
    {
        var connection = await GetConnectionAsync();
        var tokenStore = new RedisTokenStore(connection);
        var pseudonymizer = new TokenPseudonymizer(tokenStore);
        var ipAddress = "192.168.1.100";

        var token = await pseudonymizer.PseudonymizeAsync(ipAddress);
        var resolved = await pseudonymizer.ReverseAsync(token);

        resolved.Should().Be(ipAddress);
    }

    [Fact]
    public async Task DeleteTokenAsync_RemovesTokenMapping()
    {
        var connection = await GetConnectionAsync();
        var tokenStore = new RedisTokenStore(connection);
        var value = "to-delete@test.com";

        var token = await tokenStore.GetOrCreateTokenAsync(value);
        var deleted = await tokenStore.DeleteTokenAsync(token);

        deleted.Should().BeTrue();

        var action = () => tokenStore.ResolveTokenAsync(token);
        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetTokenCountAsync_ReturnsCorrectCount()
    {
        var connection = await GetConnectionAsync();
        var tokenStore = new RedisTokenStore(connection);

        var initialCount = await tokenStore.GetTokenCountAsync();

        await tokenStore.GetOrCreateTokenAsync("value1@test.com");
        await tokenStore.GetOrCreateTokenAsync("value2@test.com");

        var finalCount = await tokenStore.GetTokenCountAsync();

        finalCount.Should().Be(initialCount + 4); // 2 values, 4 keys (forward + reverse)
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsTrue()
    {
        var connection = await GetConnectionAsync();
        var tokenStore = new RedisTokenStore(connection);

        var isHealthy = await tokenStore.IsHealthyAsync();

        isHealthy.Should().BeTrue();
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
