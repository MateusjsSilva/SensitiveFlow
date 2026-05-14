using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Retention.Extensions;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Tests;

public sealed class RetentionSchedulerOptionsTests
{
    [Fact]
    public void Default_CreatesValidOptions()
    {
        var options = new RetentionSchedulerOptions { DbContextType = typeof(TestDbContext) };

        options.DbContextType.Should().Be(typeof(TestDbContext));
        options.Interval.Should().Be(TimeSpan.FromHours(1));
        options.InitialDelay.Should().Be(TimeSpan.Zero);
        options.MaxBatchSize.Should().Be(0);
        options.ContinueOnError.Should().BeTrue();
    }

    [Fact]
    public void CanConfigure_AllOptions()
    {
        var options = new RetentionSchedulerOptions
        {
            DbContextType = typeof(TestDbContext),
            Interval = TimeSpan.FromHours(2),
            InitialDelay = TimeSpan.FromMinutes(5),
            MaxBatchSize = 100,
            ContinueOnError = false
        };

        options.Interval.Should().Be(TimeSpan.FromHours(2));
        options.InitialDelay.Should().Be(TimeSpan.FromMinutes(5));
        options.MaxBatchSize.Should().Be(100);
        options.ContinueOnError.Should().BeFalse();
    }

    // Test entities
    private class TestEntity
    {
        public Guid Id { get; set; }

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        [RetentionData(Years = 1, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public string SensitiveField { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseInMemoryDatabase("test-db");
        }
    }
}

public sealed class RetentionSchedulerExtensionsTests
{
    [Fact]
    public void AddRetentionScheduler_WithGenericType_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddRetentionScheduler<TestDbContext>();

        var serviceProvider = services.BuildServiceProvider();
        var hostedServices = serviceProvider.GetServices<IHostedService>();

        hostedServices.Should().Contain(s => s is RetentionSchedulerHostedService);
    }

    [Fact]
    public void AddRetentionScheduler_WithType_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddRetentionScheduler(typeof(TestDbContext));

        var serviceProvider = services.BuildServiceProvider();
        var hostedServices = serviceProvider.GetServices<IHostedService>();

        hostedServices.Should().Contain(s => s is RetentionSchedulerHostedService);
    }

    [Fact]
    public void AddRetentionScheduler_AllowsConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddRetentionScheduler<TestDbContext>(opts =>
        {
            opts.Interval = TimeSpan.FromHours(2);
            opts.InitialDelay = TimeSpan.FromMinutes(10);
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<RetentionSchedulerOptions>();

        options.Interval.Should().Be(TimeSpan.FromHours(2));
        options.InitialDelay.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void AddRetentionScheduler_WithoutConfiguration_UsesDefaults()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddRetentionScheduler<TestDbContext>();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<RetentionSchedulerOptions>();

        options.Interval.Should().Be(TimeSpan.FromHours(1));
        options.InitialDelay.Should().Be(TimeSpan.Zero);
    }

    // Test entities
    private class TestEntity
    {
        public Guid Id { get; set; }

        [RetentionData(Years = 1, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public string SensitiveField { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }

    private class TestDbContext : DbContext
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseInMemoryDatabase("test-db-" + Guid.NewGuid());
        }
    }
}
