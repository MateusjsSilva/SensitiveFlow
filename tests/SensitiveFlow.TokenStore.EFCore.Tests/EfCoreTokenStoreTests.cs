using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.TokenStore.EFCore;
using SensitiveFlow.TokenStore.EFCore.Extensions;
using SensitiveFlow.TokenStore.EFCore.Stores;

namespace SensitiveFlow.TokenStore.EFCore.Tests;

public sealed class EfCoreTokenStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<TokenDbContext>(options => options.UseSqlite(_connection));
        services.AddSingleton<ITokenStore>(sp =>
            new EfCoreTokenStore<TokenDbContext>(
                sp.GetRequiredService<IDbContextFactory<TokenDbContext>>(),
                static ctx => ctx.TokenMappings));

        _provider = services.BuildServiceProvider(validateScopes: true);

        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
        await _connection.DisposeAsync();
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
    public async Task GetOrCreateToken_DifferentValues_ProduceDifferentTokens()
    {
        var store = _provider.GetRequiredService<ITokenStore>();

        var token1 = await store.GetOrCreateTokenAsync("value-a");
        var token2 = await store.GetOrCreateTokenAsync("value-b");

        token1.Should().NotBe(token2);
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
    public async Task GetOrCreateToken_EmptyValue_ThrowsArgumentException()
    {
        var store = _provider.GetRequiredService<ITokenStore>();

        var act = () => store.GetOrCreateTokenAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveToken_EmptyToken_ThrowsArgumentException()
    {
        var store = _provider.GetRequiredService<ITokenStore>();

        var act = () => store.ResolveTokenAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetOrCreateToken_ConcurrentCallers_ReturnSameToken()
    {
        var store = _provider.GetRequiredService<ITokenStore>();

        // Simulate concurrent access by launching multiple tasks
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => store.GetOrCreateTokenAsync("concurrent-value"))
            .ToArray();

        var tokens = await Task.WhenAll(tasks);

        tokens.Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task DI_AddEfCoreTokenStore_RegistersTokenStoreAndPseudonymizer()
    {
        await _provider.DisposeAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<TokenDbContext>(options => options.UseSqlite(_connection));
        services.AddEfCoreTokenStore<TokenDbContext>();

        _provider = services.BuildServiceProvider(validateScopes: true);

        using var scope = _provider.CreateScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<ITokenStore>();
        tokenStore.Should().BeOfType<EfCoreTokenStore<TokenDbContext>>();

        var pseudonymizer = scope.ServiceProvider.GetRequiredService<IPseudonymizer>();
        pseudonymizer.Should().NotBeNull();
    }

    [Fact]
    public async Task DI_AddEfCoreTokenStore_Dedicated_RegistersCorrectly()
    {
        await _provider.DisposeAsync();

        var services = new ServiceCollection();
        services.AddEfCoreTokenStore(options => options.UseSqlite(_connection));

        _provider = services.BuildServiceProvider(validateScopes: true);

        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        using var scope = _provider.CreateScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<ITokenStore>();
        tokenStore.Should().BeOfType<EfCoreTokenStore<TokenDbContext>>();

        var pseudonymizer = scope.ServiceProvider.GetRequiredService<IPseudonymizer>();
        pseudonymizer.Should().NotBeNull();
    }
}
