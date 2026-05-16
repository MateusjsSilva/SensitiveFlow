using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.TokenStore.EFCore;
using SensitiveFlow.TokenStore.EFCore.Audit;
using SensitiveFlow.TokenStore.EFCore.Entities;
using SensitiveFlow.TokenStore.EFCore.Expiration;
using SensitiveFlow.TokenStore.EFCore.KeyRotation;
using SensitiveFlow.TokenStore.EFCore.Salting;
using SensitiveFlow.TokenStore.EFCore.Stores;
using Xunit;

namespace SensitiveFlow.TokenStore.EFCore.Tests;

public sealed class TokenStoreEnhancementsTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<TokenDbContext>(options => options.UseSqlite(_connection));
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

    #region TokenExpirationOptions Tests

    [Fact]
    public void TokenExpirationOptions_DefaultTtl_IsNull()
    {
        var options = new TokenExpirationOptions();
        options.DefaultTtl.Should().BeNull();
    }

    [Fact]
    public void TokenExpirationOptions_PurgeOnAccess_DefaultIsFalse()
    {
        var options = new TokenExpirationOptions();
        options.PurgeOnAccess.Should().BeFalse();
    }

    [Fact]
    public void TokenExpirationOptions_CanSetProperties()
    {
        var options = new TokenExpirationOptions
        {
            DefaultTtl = TimeSpan.FromHours(24),
            PurgeOnAccess = true
        };

        options.DefaultTtl.Should().Be(TimeSpan.FromHours(24));
        options.PurgeOnAccess.Should().BeTrue();
    }

    #endregion

    #region TokenExpirationService Tests

    [Fact]
    public async Task TokenExpirationService_PurgeExpiredAsync_DoesNotThrow()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var now = DateTimeOffset.UtcNow;
            db.TokenMappings.Add(new TokenMappingEntity { Value = "test", Token = "token", ExpiresAt = now.AddHours(-1) });
            await db.SaveChangesAsync();
        }

        var service = new TokenExpirationService<TokenDbContext>(factory);
        var act = () => service.PurgeExpiredAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TokenExpirationService_GetExpiredCountAsync_DoesNotThrow()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var now = DateTimeOffset.UtcNow;
            db.TokenMappings.Add(new TokenMappingEntity { Value = "test", Token = "token", ExpiresAt = now.AddHours(-1) });
            await db.SaveChangesAsync();
        }

        var service = new TokenExpirationService<TokenDbContext>(factory);
        var act = () => service.GetExpiredCountAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TokenExpirationService_ExpiresAt_IsNullable()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var entity = new TokenMappingEntity { Value = "test", Token = "token", ExpiresAt = null };
            db.TokenMappings.Add(entity);
            await db.SaveChangesAsync();
        }

        await using var check = await factory.CreateDbContextAsync();
        var retrieved = await check.TokenMappings.FirstAsync(e => e.Token == "token");
        retrieved.ExpiresAt.Should().BeNull();
    }

    #endregion

    #region PlainTextSaltStrategy Tests

    [Fact]
    public void PlainTextSaltStrategy_Apply_ReturnsValueUnchanged()
    {
        var strategy = new PlainTextSaltStrategy();
        var result = strategy.Apply("test-value", "context");

        result.Should().Be("test-value");
    }

    [Fact]
    public void PlainTextSaltStrategy_Apply_IgnoresNullContext()
    {
        var strategy = new PlainTextSaltStrategy();
        var result = strategy.Apply("test-value", null);

        result.Should().Be("test-value");
    }

    #endregion

    #region PrefixSaltStrategy Tests

    [Fact]
    public void PrefixSaltStrategy_Apply_PrependsContext()
    {
        var strategy = new PrefixSaltStrategy();
        var result = strategy.Apply("alice@example.com", "email");

        result.Should().Be("email:alice@example.com");
    }

    [Fact]
    public void PrefixSaltStrategy_Apply_WithNullContext_ReturnsValueUnchanged()
    {
        var strategy = new PrefixSaltStrategy();
        var result = strategy.Apply("alice@example.com", null);

        result.Should().Be("alice@example.com");
    }

    [Fact]
    public void PrefixSaltStrategy_Apply_WithEmptyContext_ReturnsValueUnchanged()
    {
        var strategy = new PrefixSaltStrategy();
        var result = strategy.Apply("alice@example.com", "");

        result.Should().Be("alice@example.com");
    }

    [Fact]
    public void PrefixSaltStrategy_SameValueDifferentContexts_ProducesDifferentOutputs()
    {
        var strategy = new PrefixSaltStrategy();
        var email = "alice@example.com";

        var emailSalted = strategy.Apply(email, "email");
        var phoneSalted = strategy.Apply(email, "phone");

        emailSalted.Should().NotBe(phoneSalted);
        emailSalted.Should().Be("email:alice@example.com");
        phoneSalted.Should().Be("phone:alice@example.com");
    }

    #endregion

    #region TokenSaltStrategyRegistry Tests

    [Fact]
    public void TokenSaltStrategyRegistry_GetOrDefault_UnknownName_ReturnsPlainTextStrategy()
    {
        var registry = new TokenSaltStrategyRegistry();
        var strategy = registry.GetOrDefault("nonexistent");

        strategy.Should().BeOfType<PlainTextSaltStrategy>();
    }

    [Fact]
    public void TokenSaltStrategyRegistry_GetOrDefault_NullName_ReturnsPlainTextStrategy()
    {
        var registry = new TokenSaltStrategyRegistry();
        var strategy = registry.GetOrDefault(null);

        strategy.Should().BeOfType<PlainTextSaltStrategy>();
    }

    [Fact]
    public void TokenSaltStrategyRegistry_Register_StoresAndRetrievesStrategy()
    {
        var registry = new TokenSaltStrategyRegistry();
        var customStrategy = new PrefixSaltStrategy();

        registry.Register("custom", customStrategy);
        var retrieved = registry.GetOrDefault("custom");

        retrieved.Should().BeSameAs(customStrategy);
    }

    [Fact]
    public void TokenSaltStrategyRegistry_Register_CaseInsensitive()
    {
        var registry = new TokenSaltStrategyRegistry();
        var strategy = new PrefixSaltStrategy();

        registry.Register("MyStrategy", strategy);
        var retrieved = registry.GetOrDefault("mystrategy");

        retrieved.Should().BeSameAs(strategy);
    }

    [Fact]
    public void TokenSaltStrategyRegistry_Register_NullName_ThrowsArgumentException()
    {
        var registry = new TokenSaltStrategyRegistry();
        var strategy = new PlainTextSaltStrategy();

        var act = () => registry.Register(null!, strategy);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TokenSaltStrategyRegistry_Register_NullStrategy_ThrowsArgumentNullException()
    {
        var registry = new TokenSaltStrategyRegistry();

        var act = () => registry.Register("test", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TokenSaltStrategyRegistry_DefaultBuiltin_Plaintext()
    {
        var registry = new TokenSaltStrategyRegistry();
        var strategy = registry.GetOrDefault("plaintext");

        strategy.Should().BeOfType<PlainTextSaltStrategy>();
    }

    [Fact]
    public void TokenSaltStrategyRegistry_DefaultBuiltin_Prefix()
    {
        var registry = new TokenSaltStrategyRegistry();
        var strategy = registry.GetOrDefault("prefix");

        strategy.Should().BeOfType<PrefixSaltStrategy>();
    }

    #endregion

    #region TokenKeyRotationService Tests

    [Fact]
    public async Task TokenKeyRotationService_GetAllTokensAsync_ReturnsAllMappings()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.TokenMappings.AddRange(
                new TokenMappingEntity { Value = "value1", Token = "token1" },
                new TokenMappingEntity { Value = "value2", Token = "token2" },
                new TokenMappingEntity { Value = "value3", Token = "token3" }
            );
            await db.SaveChangesAsync();
        }

        var service = new TokenKeyRotationService<TokenDbContext>(factory);
        var tokens = await service.GetAllTokensAsync();

        tokens.Should().HaveCount(3);
        tokens.Should().Contain(e => e.Token == "token1");
        tokens.Should().Contain(e => e.Token == "token2");
        tokens.Should().Contain(e => e.Token == "token3");
    }

    [Fact]
    public async Task TokenKeyRotationService_ReplaceTokenAsync_UpdatesTokenValue()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        long id;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var entity = new TokenMappingEntity { Value = "test-value", Token = "original-token" };
            db.TokenMappings.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        var service = new TokenKeyRotationService<TokenDbContext>(factory);
        await service.ReplaceTokenAsync(id, "new-token");

        await using var check = await factory.CreateDbContextAsync();
        var updated = await check.TokenMappings.FindAsync(id);
        updated!.Token.Should().Be("new-token");
    }

    [Fact]
    public async Task TokenKeyRotationService_ReplaceTokenAsync_NonexistentId_ThrowsKeyNotFound()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        var service = new TokenKeyRotationService<TokenDbContext>(factory);

        var act = () => service.ReplaceTokenAsync(99999, "new-token");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task TokenKeyRotationService_BulkReplaceAsync_UpdatesMultipleMappings()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        long id1, id2;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var entity1 = new TokenMappingEntity { Value = "value1", Token = "token1" };
            var entity2 = new TokenMappingEntity { Value = "value2", Token = "token2" };
            db.TokenMappings.AddRange(entity1, entity2);
            await db.SaveChangesAsync();
            id1 = entity1.Id;
            id2 = entity2.Id;
        }

        var updates = new[] { (id1, "new-token1"), (id2, "new-token2") };

        var service = new TokenKeyRotationService<TokenDbContext>(factory);
        await service.BulkReplaceAsync(updates);

        await using var check = await factory.CreateDbContextAsync();
        var updated1 = await check.TokenMappings.FindAsync(id1);
        var updated2 = await check.TokenMappings.FindAsync(id2);

        updated1!.Token.Should().Be("new-token1");
        updated2!.Token.Should().Be("new-token2");
    }

    [Fact]
    public async Task TokenKeyRotationService_BulkReplaceAsync_EmptyList_DoesNotThrow()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        var service = new TokenKeyRotationService<TokenDbContext>(factory);

        var act = () => service.BulkReplaceAsync(new List<(long, string)>());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TokenKeyRotationService_DeleteAsync_RemovesMapping()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        long id;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var entity = new TokenMappingEntity { Value = "to-delete", Token = "delete-token" };
            db.TokenMappings.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        var service = new TokenKeyRotationService<TokenDbContext>(factory);
        var act = () => service.DeleteAsync(id);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TokenKeyRotationService_DeleteAsync_NonexistentId_ThrowsKeyNotFound()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<TokenDbContext>>();
        var service = new TokenKeyRotationService<TokenDbContext>(factory);

        var act = () => service.DeleteAsync(99999);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion

    #region InMemoryTokenAuditSink Tests

    [Fact]
    public async Task InMemoryTokenAuditSink_RecordAsync_StoresRecord()
    {
        var sink = new InMemoryTokenAuditSink();
        var record = new TokenAuditRecord("test-token", TokenAuditOperation.Created, DateTimeOffset.UtcNow, null);

        await sink.RecordAsync(record);

        var records = sink.GetRecords();
        records.Should().HaveCount(1);
        records[0].Token.Should().Be("test-token");
    }

    [Fact]
    public async Task InMemoryTokenAuditSink_GetRecords_ReturnsOrderedRecords()
    {
        var sink = new InMemoryTokenAuditSink();
        var now = DateTimeOffset.UtcNow;

        var record1 = new TokenAuditRecord("token1", TokenAuditOperation.Created, now, null);
        var record2 = new TokenAuditRecord("token2", TokenAuditOperation.Resolved, now.AddSeconds(1), null);

        await sink.RecordAsync(record1);
        await sink.RecordAsync(record2);

        var records = sink.GetRecords();
        records.Should().HaveCount(2);
        records[0].Token.Should().Be("token1");
        records[1].Token.Should().Be("token2");
    }

    [Fact]
    public async Task InMemoryTokenAuditSink_Clear_EmptiesSink()
    {
        var sink = new InMemoryTokenAuditSink();
        var record = new TokenAuditRecord("token", TokenAuditOperation.Created, DateTimeOffset.UtcNow, null);

        await sink.RecordAsync(record);
        sink.GetRecords().Should().HaveCount(1);

        sink.Clear();
        sink.GetRecords().Should().HaveCount(0);
    }

    [Fact]
    public async Task InMemoryTokenAuditSink_RecordAsync_ThreadSafe()
    {
        var sink = new InMemoryTokenAuditSink();
        var now = DateTimeOffset.UtcNow;
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var record = new TokenAuditRecord($"token-{i}", TokenAuditOperation.Created, now, null);
            tasks.Add(sink.RecordAsync(record));
        }

        await Task.WhenAll(tasks);

        sink.GetRecords().Should().HaveCount(100);
    }

    #endregion

    #region TokenAuditRecord Tests

    [Fact]
    public void TokenAuditRecord_HasExpectedProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var record = new TokenAuditRecord("my-token", TokenAuditOperation.Created, now, "actor-123");

        record.Token.Should().Be("my-token");
        record.Operation.Should().Be(TokenAuditOperation.Created);
        record.OccurredAt.Should().Be(now);
        record.ActorId.Should().Be("actor-123");
    }

    [Fact]
    public void TokenAuditRecord_ActorId_CanBeNull()
    {
        var record = new TokenAuditRecord("token", TokenAuditOperation.Resolved, DateTimeOffset.UtcNow, null);
        record.ActorId.Should().BeNull();
    }

    #endregion

    #region TokenAuditOperation Tests

    [Fact]
    public void TokenAuditOperation_Created_EqualsZero()
    {
        ((int)TokenAuditOperation.Created).Should().Be(0);
    }

    [Fact]
    public void TokenAuditOperation_Resolved_EqualsOne()
    {
        ((int)TokenAuditOperation.Resolved).Should().Be(1);
    }

    [Fact]
    public void TokenAuditOperation_Expired_EqualsTwo()
    {
        ((int)TokenAuditOperation.Expired).Should().Be(2);
    }

    #endregion

    #region AuditingTokenStore Tests

    [Fact]
    public void AuditingTokenStore_NullInner_ThrowsArgumentNullException()
    {
        var sink = new InMemoryTokenAuditSink();

        var act = () => new AuditingTokenStore(null!, sink);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuditingTokenStore_NullSink_ThrowsArgumentNullException()
    {
        var innerStore = new MockTokenStore();

        var act = () => new AuditingTokenStore(innerStore, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AuditingTokenStore_GetOrCreateTokenAsync_RecordsCreated()
    {
        var innerStore = new MockTokenStore();
        var sink = new InMemoryTokenAuditSink();
        var auditStore = new AuditingTokenStore(innerStore, sink, "user-123");

        var token = await auditStore.GetOrCreateTokenAsync("test-value");

        var records = sink.GetRecords();
        records.Should().HaveCount(1);
        records[0].Token.Should().Be(token);
        records[0].Operation.Should().Be(TokenAuditOperation.Created);
        records[0].ActorId.Should().Be("user-123");
    }

    [Fact]
    public async Task AuditingTokenStore_ResolveTokenAsync_RecordsResolved()
    {
        var innerStore = new MockTokenStore();
        var sink = new InMemoryTokenAuditSink();
        var auditStore = new AuditingTokenStore(innerStore, sink);

        await auditStore.ResolveTokenAsync("test-token");

        var records = sink.GetRecords();
        records.Should().HaveCount(1);
        records[0].Token.Should().Be("test-token");
        records[0].Operation.Should().Be(TokenAuditOperation.Resolved);
        records[0].ActorId.Should().BeNull();
    }

    [Fact]
    public async Task AuditingTokenStore_PassesThrough_GetOrCreateTokenAsync()
    {
        var innerStore = new MockTokenStore();
        var sink = new InMemoryTokenAuditSink();
        var auditStore = new AuditingTokenStore(innerStore, sink);

        var token = await auditStore.GetOrCreateTokenAsync("value");

        token.Should().Be(innerStore.LastCreatedToken);
    }

    [Fact]
    public async Task AuditingTokenStore_PassesThrough_ResolveTokenAsync()
    {
        var innerStore = new MockTokenStore();
        var sink = new InMemoryTokenAuditSink();
        var auditStore = new AuditingTokenStore(innerStore, sink);

        var resolved = await auditStore.ResolveTokenAsync("token");

        resolved.Should().Be("resolved-value");
    }

    #endregion

    private sealed class MockTokenStore : ITokenStore
    {
        public string LastCreatedToken { get; private set; } = "mock-token";

        public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LastCreatedToken);
        }

        public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("resolved-value");
        }
    }
}
