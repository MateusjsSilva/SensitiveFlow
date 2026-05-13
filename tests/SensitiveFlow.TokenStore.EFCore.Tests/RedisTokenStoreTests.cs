using FluentAssertions;
using Moq;
// TODO: SensitiveFlow.TokenStore.Redis project does not exist - Redis tests disabled
// using StackExchange.Redis;
// using SensitiveFlow.TokenStore.Redis;

namespace SensitiveFlow.TokenStore.Tests;

/* TODO: Redis token store tests disabled - project not available
public sealed class RedisTokenStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _redisConnectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisTokenStore _tokenStore;

    public RedisTokenStoreTests()
    {
        _redisConnectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();

        _redisConnectionMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _tokenStore = new RedisTokenStore(_redisConnectionMock.Object);
    }

    [Fact]
    public async Task GetOrCreateTokenAsync_ReturnsNewToken()
    {
        var value = "alice@example.com";
        var token = "tok_newtoken123";

        // Setup: reverse key lookup returns null (not exists)
        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("rev:")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Setup: transaction succeeds
        var transaction = new Mock<ITransaction>();
        transaction
            .Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _databaseMock
            .Setup(db => db.CreateTransaction(It.IsAny<object>()))
            .Returns(transaction.Object);

        // We need to capture the token that gets generated
        string capturedToken = "";
        transaction
            .Setup(t => t.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((k, v, _, __, ___) =>
            {
                if (k.ToString().StartsWith("tokens:") && !k.ToString().Contains("rev:"))
                {
                    capturedToken = v.ToString();
                }
            })
            .ReturnsAsync(true);

        // Act
        var result = await _tokenStore.GetOrCreateTokenAsync(value);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith("tok_");
        transaction.Verify(
            t => t.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetOrCreateTokenAsync_ReturnsExistingToken()
    {
        var value = "alice@example.com";
        var existingToken = "tok_existing456";

        // Setup: reverse key lookup returns existing token
        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("rev:")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(existingToken);

        // Act
        var result = await _tokenStore.GetOrCreateTokenAsync(value);

        // Assert
        result.Should().Be(existingToken);
        _databaseMock.Verify(
            db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("rev:")),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveTokenAsync_ReturnsValue()
    {
        var token = "tok_token789";
        var expectedValue = "alice@example.com";

        // Setup: token lookup returns value
        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"tokens:{token}"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(expectedValue);

        // Act
        var result = await _tokenStore.ResolveTokenAsync(token);

        // Assert
        result.Should().Be(expectedValue);
    }

    [Fact]
    public async Task ResolveTokenAsync_ThrowsWhenTokenNotFound()
    {
        var token = "tok_nonexistent";

        // Setup: token lookup returns null
        _databaseMock
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var action = () => _tokenStore.ResolveTokenAsync(token);

        // Assert
        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Constructor_ThrowsWhenRedisIsNull()
    {
        var action = () => new RedisTokenStore(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsWhenKeyPrefixIsNull()
    {
        var action = () => new RedisTokenStore(_redisConnectionMock.Object, null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetOrCreateTokenAsync_ThrowsWhenValueIsEmpty()
    {
        var action = () => _tokenStore.GetOrCreateTokenAsync("");

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveTokenAsync_ThrowsWhenTokenIsEmpty()
    {
        var action = () => _tokenStore.ResolveTokenAsync("");

        await action.Should().ThrowAsync<ArgumentException>();
    }
}

public sealed class RedisTokenStoreExtensionsTests
{
    [Fact]
    public void AddRedisTokenStore_WithConnection_RegistersService()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var connectionMock = new Mock<IConnectionMultiplexer>();

        services.AddRedisTokenStore(connectionMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        var tokenStore = serviceProvider.GetRequiredService<SensitiveFlow.Core.Interfaces.ITokenStore>();

        tokenStore.Should().BeOfType<RedisTokenStore>();
    }

    [Fact]
    public void AddRedisTokenStore_AllowsConfiguration()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var connectionMock = new Mock<IConnectionMultiplexer>();

        services.AddRedisTokenStore(
            connectionMock.Object,
            keyPrefix: "custom:",
            defaultExpiry: TimeSpan.FromHours(1));

        var serviceProvider = services.BuildServiceProvider();
        var tokenStore = serviceProvider.GetRequiredService<SensitiveFlow.Core.Interfaces.ITokenStore>();

        tokenStore.Should().BeOfType<RedisTokenStore>();
    }

    [Fact]
    public void AddRedisTokenStore_WithConnectionString_CreatesConnection()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // This would normally fail without a real Redis, but we're just testing registration
        // In real tests, use testcontainers or mock
        var action = () => services.AddRedisTokenStore("localhost:6379");

        // Just verify it doesn't throw during registration
        action.Should().NotThrow();
    }
}
*/
