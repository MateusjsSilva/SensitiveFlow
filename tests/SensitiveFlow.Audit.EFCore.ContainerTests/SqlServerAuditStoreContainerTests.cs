using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.EFCore.Maintenance;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using Testcontainers.MsSql;

namespace SensitiveFlow.Audit.EFCore.ContainerTests;

[Trait("Category", "Container")]
public sealed class SqlServerAuditStoreContainerTests : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private string _connectionString = string.Empty;
    private ServiceProvider _provider = null!;

    public SqlServerAuditStoreContainerTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _connectionString = _fixture.GetConnectionString();

        var services = new ServiceCollection();
        services.AddEfCoreAuditStore(options => options.UseSqlServer(_connectionString));

        _provider = services.BuildServiceProvider(validateScopes: true);

        var factory = _provider.GetRequiredService<IDbContextFactory<AuditDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_connectionString))
        {
            var services = new ServiceCollection();
            services.AddDbContextFactory<AuditDbContext>(options => options.UseSqlServer(_connectionString));
            await using var cleanupProvider = services.BuildServiceProvider(validateScopes: true);
            var factory = cleanupProvider.GetRequiredService<IDbContextFactory<AuditDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task EfCoreAuditStore_AppendAndQuery_RoundTripsOnSqlServer()
    {
        var store = _provider.GetRequiredService<IAuditStore>();

        var record = SampleRecord("subject-sql", "Email", DateTimeOffset.UtcNow);
        await store.AppendAsync(record);

        var results = await store.QueryByDataSubjectAsync("subject-sql", take: 10);

        results.Should().ContainSingle(r => r.Field == "Email" && r.DataSubjectId == "subject-sql");
    }

    [Fact]
    public async Task EfCoreAuditStore_BatchAppend_RoundTripsOnSqlServer()
    {
        var store = _provider.GetRequiredService<IAuditStore>();
        var batch = store.Should().BeAssignableTo<IBatchAuditStore>().Subject;

        var records = new[]
        {
            SampleRecord("batch-sql", "Name", DateTimeOffset.UtcNow.AddMinutes(-2)),
            SampleRecord("batch-sql", "Phone", DateTimeOffset.UtcNow.AddMinutes(-1)),
            SampleRecord("batch-sql", "TaxId", DateTimeOffset.UtcNow),
        };

        await batch.AppendRangeAsync(records);

        var results = await store.QueryByDataSubjectAsync("batch-sql", take: 10);
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task AuditLogRetention_PurgeOlderThan_WorksOnSqlServer()
    {
        var store = _provider.GetRequiredService<IAuditStore>();
        var retention = _provider.GetRequiredService<IAuditLogRetention>();

        await store.AppendAsync(SampleRecord("old-sql", "Email", DateTimeOffset.UtcNow.AddYears(-3)));
        await store.AppendAsync(SampleRecord("fresh-sql", "Email", DateTimeOffset.UtcNow));

        var deleted = await retention.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddYears(-1));

        deleted.Should().Be(1);
        var remaining = await store.QueryAsync(take: 10);
        remaining.Should().ContainSingle(r => r.DataSubjectId == "fresh-sql");
    }

    [Fact]
    public async Task RetryDecorator_ComposesOverSqlServerStore()
    {
        await _provider.DisposeAsync();

        var services = new ServiceCollection();
        services.AddEfCoreAuditStore(options => options.UseSqlServer(_connectionString));
        services.AddAuditStoreRetry(options =>
        {
            options.MaxAttempts = 3;
            options.InitialDelay = TimeSpan.FromMilliseconds(1);
        });

        _provider = services.BuildServiceProvider(validateScopes: true);

        var store = _provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<RetryingAuditStore>();

        await store.AppendAsync(SampleRecord("retry-sql", "Email", DateTimeOffset.UtcNow));

        var verifyServices = new ServiceCollection();
        verifyServices.AddEfCoreAuditStore(options => options.UseSqlServer(_connectionString));
        await using var verifyProvider = verifyServices.BuildServiceProvider(validateScopes: true);
        var durableStore = verifyProvider.GetRequiredService<IAuditStore>();

        var records = await durableStore.QueryByDataSubjectAsync("retry-sql", take: 10);
        records.Should().ContainSingle(r => r.Field == "Email");
    }

    private static AuditRecord SampleRecord(string subject, string field, DateTimeOffset timestamp) => new()
    {
        DataSubjectId = subject,
        Entity = "Customer",
        Field = field,
        Operation = AuditOperation.Update,
        Timestamp = timestamp,
        ActorId = "sqlserver-container-test",
        IpAddressToken = "ip-token",
        Details = "sql server container test",
    };
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("SensitiveFlow123!")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string GetConnectionString() => _container.GetConnectionString();
}
