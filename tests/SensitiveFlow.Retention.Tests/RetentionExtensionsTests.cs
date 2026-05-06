using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SensitiveFlow.Retention.Contracts;
using SensitiveFlow.Retention.Extensions;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Tests;

public sealed class RetentionExtensionsTests
{
    [Fact]
    public void AddRetention_RegistersRetentionEvaluator()
    {
        var services = new ServiceCollection();
        services.AddRetention();

        var provider = services.BuildServiceProvider();

        provider.GetService<RetentionEvaluator>().Should().NotBeNull();
    }

    [Fact]
    public void AddRetention_RegistersEvaluatorAsTransient()
    {
        var services = new ServiceCollection();
        services.AddRetention();

        var provider = services.BuildServiceProvider();

        var a = provider.GetRequiredService<RetentionEvaluator>();
        var b = provider.GetRequiredService<RetentionEvaluator>();

        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void AddRetentionHandler_RegistersCustomHandler()
    {
        var services = new ServiceCollection();
        services.AddRetention();
        services.AddRetentionHandler<FakeHandler>();

        var provider = services.BuildServiceProvider();

        var handlers = provider.GetServices<IRetentionExpirationHandler>();
        handlers.Should().ContainSingle(h => h is FakeHandler);
    }

    [Fact]
    public void AddRetentionHandler_MultipleHandlers_AllRegistered()
    {
        var services = new ServiceCollection();
        services.AddRetention();
        services.AddRetentionHandler<FakeHandler>();
        services.AddRetentionHandler<AnotherFakeHandler>();

        var provider = services.BuildServiceProvider();

        var handlers = provider.GetServices<IRetentionExpirationHandler>().ToList();
        handlers.Should().HaveCount(2);
    }

    [Fact]
    public void AddRetention_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddRetention();
        result.Should().BeSameAs(services);
    }

    private sealed class FakeHandler : IRetentionExpirationHandler
    {
        public Task HandleAsync(object entity, string fieldName, DateTimeOffset expiredAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class AnotherFakeHandler : IRetentionExpirationHandler
    {
        public Task HandleAsync(object entity, string fieldName, DateTimeOffset expiredAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
