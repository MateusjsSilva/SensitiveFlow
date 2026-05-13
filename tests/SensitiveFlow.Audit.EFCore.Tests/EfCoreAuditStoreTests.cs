using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Configuration;
using SensitiveFlow.Audit.EFCore.Entities;
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.EFCore.Maintenance;
using SensitiveFlow.Audit.EFCore.Stores;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.TestKit;

namespace SensitiveFlow.Audit.EFCore.Tests;

public sealed class EfCoreAuditStoreContractTests : AuditStoreContractTests, IAsyncLifetime
{
    private readonly List<SqliteConnection> _connections = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
    }

    protected override async Task<IAuditStore> CreateStoreAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);

        var factory = new TestContextFactory(connection);

        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        return new EfCoreAuditStore<AuditDbContext>(factory, static c => c.AuditRecords);
    }

    private sealed class TestContextFactory(SqliteConnection connection) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(connection)
                .Options;
            return new AuditDbContext(options);
        }
    }
}

public sealed class AuditLogRetentionTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PurgeOlderThanAsync_DeletesOldRecords()
    {
        var factory = new ConnectionScopedFactory(_connection);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        var store = new EfCoreAuditStore<AuditDbContext>(factory);

        var old = new AuditRecord
        {
            DataSubjectId = "alice",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow.AddYears(-3),
        };
        var fresh = new AuditRecord
        {
            DataSubjectId = "bob",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
        };

        await store.AppendAsync(old);
        await store.AppendAsync(fresh);

        var retention = new AuditLogRetention<AuditDbContext>(factory);
        var deleted = await retention.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddYears(-1));

        deleted.Should().Be(1);
        var remaining = await store.QueryAsync();
        remaining.Should().ContainSingle(r => r.DataSubjectId == "bob");
    }

    [Fact]
    public async Task PurgeOlderThanAsync_FallsBackWhenProviderDoesNotSupportExecuteDelete()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        var store = new EfCoreAuditStore<AuditDbContext>(factory);
        await store.AppendAsync(new AuditRecord
        {
            DataSubjectId = "old",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow.AddYears(-2),
        });

        var retention = new AuditLogRetention<AuditDbContext>(factory);

        var deleted = await retention.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddYears(-1));

        deleted.Should().Be(1);
        (await store.QueryAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PurgeOlderThanAsync_FallbackReturnsZeroWhenNothingMatches()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        var retention = new AuditLogRetention<AuditDbContext>(factory);

        var deleted = await retention.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddYears(-1));

        deleted.Should().Be(0);
    }

    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        var act = () => new AuditLogRetention<AuditDbContext>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class ConnectionScopedFactory(SqliteConnection connection) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(connection)
                .Options;
            return new AuditDbContext(options);
        }
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
            return new AuditDbContext(options);
        }
    }
}

public sealed class EfCoreAuditStoreEdgeCaseTests
{
    [Fact]
    public void AuditRecordEntity_Id_IsMutableForEfCoreMaterialization()
    {
        var entity = new AuditRecordEntity { Id = 42 };

        entity.Id.Should().Be(42);
    }

    [Fact]
    public void AuditRecordEntityTypeConfiguration_RejectsBlankTableName()
    {
        var act = () => new AuditRecordEntityTypeConfiguration(" ");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tableName");
    }

    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        var act = () => new EfCoreAuditStore<AuditDbContext>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendAsync_RejectsNullRecord()
    {
        var store = new EfCoreAuditStore<AuditDbContext>(new InMemoryFactory());

        var act = () => store.AppendAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendRangeAsync_RejectsNullRecords()
    {
        var store = new EfCoreAuditStore<AuditDbContext>(new InMemoryFactory());

        var act = () => store.AppendRangeAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendRangeAsync_ReturnsWithoutCreatingRows_WhenRecordsAreEmpty()
    {
        var factory = new InMemoryFactory();
        var store = new EfCoreAuditStore<AuditDbContext>(factory);

        await store.AppendRangeAsync([]);

        (await store.QueryAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_AppliesDateRangeAndPagination()
    {
        var factory = new InMemoryFactory();
        var store = new EfCoreAuditStore<AuditDbContext>(factory);
        var now = DateTimeOffset.UtcNow;

        await store.AppendRangeAsync([
            Record("a", now.AddHours(-3)),
            Record("b", now.AddHours(-2)),
            Record("c", now.AddHours(-1)),
        ]);

        var result = await store.QueryAsync(now.AddHours(-3), now, skip: 1, take: 1);

        result.Should().ContainSingle(r => r.DataSubjectId == "b");
    }

    [Fact]
    public async Task QueryByDataSubjectAsync_AppliesSubjectDateRangeAndPagination()
    {
        var factory = new InMemoryFactory();
        var store = new EfCoreAuditStore<AuditDbContext>(factory);
        var now = DateTimeOffset.UtcNow;

        await store.AppendRangeAsync([
            Record("alice", now.AddHours(-3)),
            Record("alice", now.AddHours(-2)),
            Record("alice", now.AddHours(-1)),
            Record("bob", now.AddHours(-1)),
        ]);

        var result = await store.QueryByDataSubjectAsync("alice", now.AddHours(-3), now, skip: 1, take: 1);

        result.Should().ContainSingle(r => r.DataSubjectId == "alice" && r.Timestamp == now.AddHours(-2));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, -1)]
    public async Task QueryAsync_RejectsInvalidPagination(int skip, int take)
    {
        var store = new EfCoreAuditStore<AuditDbContext>(new InMemoryFactory());

        var act = () => store.QueryAsync(skip: skip, take: take);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task QueryByDataSubjectAsync_RejectsNullOrEmptySubject()
    {
        var store = new EfCoreAuditStore<AuditDbContext>(new InMemoryFactory());

        var act = () => store.QueryByDataSubjectAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, -1)]
    public async Task QueryByDataSubjectAsync_RejectsInvalidPagination(int skip, int take)
    {
        var store = new EfCoreAuditStore<AuditDbContext>(new InMemoryFactory());

        var act = () => store.QueryByDataSubjectAsync("subject", skip: skip, take: take);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_InvokesOperation()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var factory = new SqliteFactory(connection);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        var store = new EfCoreAuditStore<AuditDbContext>(factory);
        var called = false;

        await store.ExecuteInTransactionAsync(ct =>
        {
            called = !ct.IsCancellationRequested;
            return Task.CompletedTask;
        });

        called.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RejectsNullOperation()
    {
        var store = new EfCoreAuditStore<AuditDbContext>(new InMemoryFactory());

        var act = () => store.ExecuteInTransactionAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RethrowsOperationFailure()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var factory = new SqliteFactory(connection);
        await using (var ctx = factory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        var store = new EfCoreAuditStore<AuditDbContext>(factory);
        var failure = new InvalidOperationException("boom");

        var act = () => store.ExecuteInTransactionAsync(_ => throw failure);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
    }

    private static AuditRecord Record(string subject, DateTimeOffset timestamp) => new()
    {
        DataSubjectId = subject,
        Entity = "Customer",
        Field = "Email",
        Operation = AuditOperation.Update,
        Timestamp = timestamp,
    };

    private sealed class InMemoryFactory : IDbContextFactory<AuditDbContext>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");

        public AuditDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            return new AuditDbContext(options);
        }
    }

    private sealed class SqliteFactory(SqliteConnection connection) : IDbContextFactory<AuditDbContext>
    {
        public AuditDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(connection)
                .Options;
            return new AuditDbContext(options);
        }
    }
}

/// <summary>
/// End-to-end test of <see cref="IAuditLogRetention"/> resolved through the public DI
/// extension <c>AddEfCoreAuditStore</c>. Validates that the interface (the contract a
/// background job would actually depend on) is wired correctly to the concrete
/// <see cref="AuditLogRetention{TContext}"/> backed by the same store.
/// </summary>
public sealed class IAuditLogRetentionViaDITests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        // Register the audit store against the open SQLite connection. AddEfCoreAuditStore
        // also registers IAuditLogRetention pointing at the same factory.
        services.AddEfCoreAuditStore(opt => opt.UseSqlite(_connection));

        _provider = services.BuildServiceProvider(validateScopes: true);

        // Create schema using a context resolved through the same factory.
        var factory = _provider.GetRequiredService<IDbContextFactory<AuditDbContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public void IAuditLogRetention_IsResolvable()
    {
        var retention = _provider.GetRequiredService<IAuditLogRetention>();
        retention.Should().NotBeNull();
    }

    [Fact]
    public async Task IAuditLogRetention_PurgesOnlyExpiredRecords()
    {
        var store = _provider.GetRequiredService<IAuditStore>();
        var retention = _provider.GetRequiredService<IAuditLogRetention>();

        await store.AppendAsync(new AuditRecord
        {
            DataSubjectId = "old-subject",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow.AddYears(-3),
        });
        await store.AppendAsync(new AuditRecord
        {
            DataSubjectId = "fresh-subject",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
        });

        var deleted = await retention.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddYears(-1));

        deleted.Should().Be(1);

        var remaining = await store.QueryAsync();
        remaining.Should().ContainSingle(r => r.DataSubjectId == "fresh-subject");
    }

    [Fact]
    public async Task IAuditLogRetention_OnEmptyStore_ReturnsZero()
    {
        var retention = _provider.GetRequiredService<IAuditLogRetention>();

        var deleted = await retention.PurgeOlderThanAsync(DateTimeOffset.UtcNow);

        deleted.Should().Be(0);
    }

    [Fact]
    public void AddEfCoreAuditStore_RejectsNullOptionsAction()
    {
        var services = new ServiceCollection();

        var act = () => services.AddEfCoreAuditStore(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddEfCoreAuditStore_Generic_RegistersStoreAndRetentionForExistingContextFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AuditDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));

        services.AddEfCoreAuditStore<AuditDbContext>();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuditStore>().Should().BeOfType<EfCoreAuditStore<AuditDbContext>>();
        provider.GetRequiredService<IAuditLogRetention>().Should().BeOfType<AuditLogRetention<AuditDbContext>>();
    }
}
