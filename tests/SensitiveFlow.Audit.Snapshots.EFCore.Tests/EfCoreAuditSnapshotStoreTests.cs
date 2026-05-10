using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Snapshots.EFCore;
using SensitiveFlow.Audit.Snapshots.EFCore.Configuration;
using SensitiveFlow.Audit.Snapshots.EFCore.Entities;
using SensitiveFlow.Audit.Snapshots.EFCore.Extensions;
using SensitiveFlow.Audit.Snapshots.EFCore.Stores;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Snapshots.EFCore.Tests;

public sealed class EfCoreAuditSnapshotStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<SnapshotDbContext>(options => options.UseSqlite(_connection));
        services.AddSingleton<IAuditSnapshotStore>(sp =>
            new EfCoreAuditSnapshotStore<SnapshotDbContext>(
                sp.GetRequiredService<IDbContextFactory<SnapshotDbContext>>(),
                static ctx => ctx.AuditSnapshots));

        _provider = services.BuildServiceProvider(validateScopes: true);

        var factory = _provider.GetRequiredService<IDbContextFactory<SnapshotDbContext>>();
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
    public async Task AppendAndQueryByAggregate_RoundTrips()
    {
        var store = _provider.GetRequiredService<IAuditSnapshotStore>();

        var snapshot = new AuditSnapshot
        {
            DataSubjectId = "subject-1",
            Aggregate = "Customer",
            AggregateId = "42",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "tester",
            BeforeJson = """{"Name":"Old"}""",
            AfterJson = """{"Name":"New"}""",
        };

        await store.AppendAsync(snapshot);

        var results = await store.QueryByAggregateAsync("Customer", "42", take: 10);

        results.Should().ContainSingle();
        results[0].DataSubjectId.Should().Be("subject-1");
        results[0].BeforeJson.Should().Be("""{"Name":"Old"}""");
        results[0].AfterJson.Should().Be("""{"Name":"New"}""");
    }

    [Fact]
    public async Task QueryByAggregate_FiltersByTimeRange()
    {
        var store = _provider.GetRequiredService<IAuditSnapshotStore>();

        var old = new AuditSnapshot
        {
            DataSubjectId = "subject-2",
            Aggregate = "Order",
            AggregateId = "100",
            Operation = AuditOperation.Create,
            Timestamp = DateTimeOffset.UtcNow.AddDays(-10),
            ActorId = "tester",
        };

        var recent = new AuditSnapshot
        {
            DataSubjectId = "subject-2",
            Aggregate = "Order",
            AggregateId = "100",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "tester",
        };

        await store.AppendAsync(old);
        await store.AppendAsync(recent);

        var results = await store.QueryByAggregateAsync(
            "Order", "100",
            from: DateTimeOffset.UtcNow.AddDays(-5),
            take: 10);

        results.Should().ContainSingle(s => s.Operation == AuditOperation.Update);
    }

    [Fact]
    public async Task QueryByDataSubject_ReturnsAllAggregates()
    {
        var store = _provider.GetRequiredService<IAuditSnapshotStore>();

        await store.AppendAsync(new AuditSnapshot
        {
            DataSubjectId = "subject-3",
            Aggregate = "Customer",
            AggregateId = "1",
            Operation = AuditOperation.Create,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "tester",
        });

        await store.AppendAsync(new AuditSnapshot
        {
            DataSubjectId = "subject-3",
            Aggregate = "Order",
            AggregateId = "99",
            Operation = AuditOperation.Create,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "tester",
        });

        var results = await store.QueryByDataSubjectAsync("subject-3", take: 10);

        results.Should().HaveCount(2);
        results.Select(s => s.Aggregate).Should().BeEquivalentTo("Customer", "Order");
    }

    [Fact]
    public async Task Append_NullSnapshot_ThrowsArgumentNull()
    {
        var store = _provider.GetRequiredService<IAuditSnapshotStore>();

        var act = () => store.AppendAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryByAggregate_EmptyAggregate_ThrowsArgumentException()
    {
        var store = _provider.GetRequiredService<IAuditSnapshotStore>();

        var act = () => store.QueryByAggregateAsync("", "42");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DI_AddEfCoreAuditSnapshotStore_Dedicated_RegistersCorrectly()
    {
        await _provider.DisposeAsync();

        var services = new ServiceCollection();
        services.AddEfCoreAuditSnapshotStore(options => options.UseSqlite(_connection));

        _provider = services.BuildServiceProvider(validateScopes: true);

        var factory = _provider.GetRequiredService<IDbContextFactory<SnapshotDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        var store = _provider.GetRequiredService<IAuditSnapshotStore>();
        store.Should().BeOfType<EfCoreAuditSnapshotStore<SnapshotDbContext>>();
    }

    [Fact]
    public async Task DI_AddEfCoreAuditSnapshotStore_Generic_RegistersCorrectly()
    {
        await _provider.DisposeAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<SnapshotDbContext>(options => options.UseSqlite(_connection));
        services.AddEfCoreAuditSnapshotStore<SnapshotDbContext>();

        _provider = services.BuildServiceProvider(validateScopes: true);

        var store = _provider.GetRequiredService<IAuditSnapshotStore>();
        store.Should().BeOfType<EfCoreAuditSnapshotStore<SnapshotDbContext>>();
    }

    [Fact]
    public void AuditSnapshotEntity_Id_IsMutableForEfCoreMaterialization()
    {
        var entity = new AuditSnapshotEntity { Id = 42 };

        entity.Id.Should().Be(42);
    }

    [Fact]
    public void AuditSnapshotEntityTypeConfiguration_RejectsBlankTableName()
    {
        var act = () => new AuditSnapshotEntityTypeConfiguration(" ");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tableName");
    }
}
