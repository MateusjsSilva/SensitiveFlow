using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.EFCore.Maintenance;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using Testcontainers.PostgreSql;

namespace SensitiveFlow.Audit.EFCore.ContainerTests;

[Trait("Category", "Container")]
public sealed class PostgresAuditStoreContainerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private string _connectionString = string.Empty;
    private ServiceProvider _provider = null!;

    public PostgresAuditStoreContainerTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _connectionString = _fixture.CreateDatabaseConnectionString();

        var services = new ServiceCollection();
        services.AddEfCoreAuditStore(options => options.UseNpgsql(_connectionString));

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
            services.AddDbContextFactory<AuditDbContext>(options => options.UseNpgsql(_connectionString));
            await using var cleanupProvider = services.BuildServiceProvider(validateScopes: true);
            var factory = cleanupProvider.GetRequiredService<IDbContextFactory<AuditDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task EfCoreAuditStore_AppendRangeAndQuery_RoundTripsOnPostgres()
    {
        var store = _provider.GetRequiredService<IAuditStore>();
        var batch = store.Should().BeAssignableTo<IBatchAuditStore>().Subject;

        var records = new[]
        {
            SampleRecord("subject-a", "Email", DateTimeOffset.UtcNow.AddMinutes(-2)),
            SampleRecord("subject-a", "TaxId", DateTimeOffset.UtcNow.AddMinutes(-1)),
            SampleRecord("subject-b", "Name", DateTimeOffset.UtcNow),
        };

        await batch.AppendRangeAsync(records);

        var subjectA = await store.QueryByDataSubjectAsync("subject-a", take: 10);

        subjectA.Should().HaveCount(2);
        subjectA.Select(r => r.Field).Should().BeEquivalentTo("Email", "TaxId");
    }

    [Fact]
    public async Task AuditLogRetention_PurgeOlderThan_UsesRelationalProvider()
    {
        var store = _provider.GetRequiredService<IAuditStore>();
        var retention = _provider.GetRequiredService<IAuditLogRetention>();

        await store.AppendAsync(SampleRecord("old-subject", "Email", DateTimeOffset.UtcNow.AddYears(-3)));
        await store.AppendAsync(SampleRecord("fresh-subject", "Email", DateTimeOffset.UtcNow));

        var deleted = await retention.PurgeOlderThanAsync(DateTimeOffset.UtcNow.AddYears(-1));

        deleted.Should().Be(1);
        var remaining = await store.QueryAsync(take: 10);
        remaining.Should().ContainSingle(r => r.DataSubjectId == "fresh-subject");
    }

    [Fact]
    public async Task RetryAndBufferedDecorators_ComposeOverPostgresStore()
    {
        await _provider.DisposeAsync();

        var services = new ServiceCollection();
        services.AddEfCoreAuditStore(options => options.UseNpgsql(_connectionString));
        services.AddAuditStoreRetry(options => options.InitialDelay = TimeSpan.FromMilliseconds(1));
        services.AddBufferedAuditStore(options =>
        {
            options.Capacity = 32;
            options.MaxBatchSize = 8;
        });

        _provider = services.BuildServiceProvider(validateScopes: true);

        var store = _provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStore>();

        await store.AppendAsync(SampleRecord("subject-buffer", "Email", DateTimeOffset.UtcNow));
        await ((BufferedAuditStore)store).DisposeAsync();

        var verifyServices = new ServiceCollection();
        verifyServices.AddEfCoreAuditStore(options => options.UseNpgsql(_connectionString));
        await using var verifyProvider = verifyServices.BuildServiceProvider(validateScopes: true);
        var durableStore = verifyProvider.GetRequiredService<IAuditStore>();

        var records = await durableStore.QueryByDataSubjectAsync("subject-buffer", take: 10);
        records.Should().ContainSingle(r => r.Field == "Email");
    }

    private static AuditRecord SampleRecord(string subject, string field, DateTimeOffset timestamp) => new()
    {
        DataSubjectId = subject,
        Entity = "Customer",
        Field = field,
        Operation = AuditOperation.Update,
        Timestamp = timestamp,
        ActorId = "container-test",
        IpAddressToken = "ip-token",
        Details = "postgres container test",
    };
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string CreateDatabaseConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "sf_" + Guid.NewGuid().ToString("N"),
        };

        return builder.ConnectionString;
    }
}
