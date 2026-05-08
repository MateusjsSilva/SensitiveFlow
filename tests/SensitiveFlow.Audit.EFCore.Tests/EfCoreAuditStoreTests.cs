using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SensitiveFlow.Audit.EFCore;
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
