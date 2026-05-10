using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class AuditStoreExtensionsTests
{
    [Fact]
    public void AddAuditStore_RegistersIAuditStore()
    {
        var services = new ServiceCollection();
        services.AddAuditStore<FakeAuditStore>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IAuditStore>().Should().BeOfType<FakeAuditStore>();
    }

    [Fact]
    public void AddAuditStore_RegistersAsScoped()
    {
        var services = new ServiceCollection();
        services.AddAuditStore<FakeAuditStore>();

        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var a = scope1.ServiceProvider.GetRequiredService<IAuditStore>();
        var b = scope1.ServiceProvider.GetRequiredService<IAuditStore>();
        var c = scope2.ServiceProvider.GetRequiredService<IAuditStore>();

        a.Should().BeSameAs(b);
        a.Should().NotBeSameAs(c);
    }

    [Fact]
    public void AddAuditStore_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddAuditStore<FakeAuditStore>();
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddAuditStoreRetry_WithoutAuditStore_ThrowsHelpfulError()
    {
        var services = new ServiceCollection();

        var act = () => services.AddAuditStoreRetry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddAuditStore<T>()*AddAuditStoreRetry*");
    }

    [Fact]
    public void AddAuditStoreRetry_WrapsFactoryRegistration()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAuditStore>(_ => new FakeAuditStore());

        services.AddAuditStoreRetry(options => options.MaxAttempts = 1);
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuditStore>()
            .Should().BeOfType<RetryingAuditStore>();
    }

    [Fact]
    public void AddAuditStoreRetry_WrapsImplementationTypeRegistration()
    {
        var services = new ServiceCollection();
        services.AddAuditStore<FakeAuditStore>();

        services.AddAuditStoreRetry(options => options.MaxAttempts = 1);
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuditStore>()
            .Should().BeOfType<RetryingAuditStore>();
    }

    [Fact]
    public void AddAuditStoreRetry_WrapsSingletonInstanceRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore>(new FakeAuditStore());

        services.AddAuditStoreRetry();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuditStore>()
            .Should().BeOfType<RetryingAuditStore>();
    }

    [Fact]
    public void AddBufferedAuditStore_WithoutAuditStore_ThrowsHelpfulError()
    {
        var services = new ServiceCollection();

        var act = () => services.AddBufferedAuditStore();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddAuditStore<T>()*AddBufferedAuditStore*");
    }

    [Fact]
    public void AddBufferedAuditStore_RejectsNonSingletonStore()
    {
        var services = new ServiceCollection();
        services.AddAuditStore<FakeAuditStore>();

        var act = () => services.AddBufferedAuditStore();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires a Singleton*");
    }

    [Fact]
    public async Task AddBufferedAuditStore_WrapsSingletonFactoryRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore>(_ => new FakeAuditStore());

        services.AddBufferedAuditStore(options => options.Capacity = 8);
        await using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IAuditStore>();

        store.Should().BeOfType<BufferedAuditStore>();
    }

    [Fact]
    public async Task AddBufferedAuditStore_WrapsSingletonInstanceRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore>(new FakeAuditStore());

        services.AddBufferedAuditStore();
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuditStore>()
            .Should().BeOfType<BufferedAuditStore>();
    }

    private sealed class FakeAuditStore : IAuditStore
    {
        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(
            DateTimeOffset? from = null, DateTimeOffset? to = null,
            int skip = 0, int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>([]);

        public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
            string dataSubjectId,
            DateTimeOffset? from = null, DateTimeOffset? to = null,
            int skip = 0, int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>([]);
    }
}
