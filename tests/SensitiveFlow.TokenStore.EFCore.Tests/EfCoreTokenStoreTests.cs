using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.TokenStore.EFCore;
using SensitiveFlow.TokenStore.EFCore.Configuration;
using SensitiveFlow.TokenStore.EFCore.Entities;
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

    [Fact]
    public void DI_AddEfCoreTokenStore_Dedicated_RejectsNullOptionsAction()
    {
        var services = new ServiceCollection();

        var act = () => services.AddEfCoreTokenStore(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public sealed class EfCoreTokenStoreEdgeCaseTests
{
    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        var act = () => new EfCoreTokenStore<TokenDbContext>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TokenMappingEntity_Id_IsMutableForEfCoreMaterialization()
    {
        var entity = new TokenMappingEntity { Id = 42 };

        entity.Id.Should().Be(42);
    }

    [Fact]
    public void TokenMappingEntityTypeConfiguration_RejectsBlankTableName()
    {
        var act = () => new TokenMappingEntityTypeConfiguration(" ");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tableName");
    }

    [Fact]
    public async Task GetOrCreateToken_RecoversFromConcurrentInsert()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var normalOptions = new DbContextOptionsBuilder<TokenDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var db = new TokenDbContext(normalOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var factory = new RaceFactory(connection);
        var store = new EfCoreTokenStore<RaceTokenDbContext>(factory, static ctx => ctx.TokenMappings);

        var token = await store.GetOrCreateTokenAsync("raced-value");

        token.Should().Be("winner-token");
    }

    private sealed class RaceFactory(SqliteConnection connection) : IDbContextFactory<RaceTokenDbContext>
    {
        public RaceTokenDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<TokenDbContext>()
                .UseSqlite(connection)
                .Options;
            return new RaceTokenDbContext(options, connection);
        }
    }

    private sealed class RaceTokenDbContext(
        DbContextOptions<TokenDbContext> options,
        SqliteConnection connection) : TokenDbContext(options)
    {
        private bool _hasThrown;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var pending = ChangeTracker.Entries<TokenMappingEntity>()
                .FirstOrDefault(e => e.State == EntityState.Added);

            if (pending is not null && !_hasThrown)
            {
                _hasThrown = true;
                var normalOptions = new DbContextOptionsBuilder<TokenDbContext>()
                    .UseSqlite(connection)
                    .Options;
                await using var winnerContext = new TokenDbContext(normalOptions);
                winnerContext.TokenMappings.Add(new TokenMappingEntity
                {
                    Value = pending.Entity.Value,
                    Token = "winner-token",
                });
                await winnerContext.SaveChangesAsync(cancellationToken);

                throw new DbUpdateException("Simulated unique index race.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
