using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.EFCore;
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
}
