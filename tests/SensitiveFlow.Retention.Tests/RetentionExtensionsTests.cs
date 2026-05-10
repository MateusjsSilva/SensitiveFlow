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
    public void AddRetention_RegistersEvaluatorAsScoped()
    {
        var services = new ServiceCollection();
        services.AddRetention();

        var provider = services.BuildServiceProvider();

        // Within the same scope, both resolutions should return the same instance.
        using var scope = provider.CreateScope();
        var a = scope.ServiceProvider.GetRequiredService<RetentionEvaluator>();
        var b = scope.ServiceProvider.GetRequiredService<RetentionEvaluator>();
        a.Should().BeSameAs(b);

        // Across scopes, a fresh instance is created.
        using var scope2 = provider.CreateScope();
        var c = scope2.ServiceProvider.GetRequiredService<RetentionEvaluator>();
        a.Should().NotBeSameAs(c);
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

    [Fact]
    public void AddRetentionExecutor_RegistersRetentionExecutorAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddRetentionExecutor();

        var provider = services.BuildServiceProvider();

        var executor = provider.GetService<RetentionExecutor>();
        executor.Should().NotBeNull();

        var a = provider.GetRequiredService<RetentionExecutor>();
        var b = provider.GetRequiredService<RetentionExecutor>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void AddRetentionExecutor_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddRetentionExecutor(options =>
        {
            options.AnonymousStringMarker = "CUSTOM";
        });

        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<RetentionExecutorOptions>();
        options.AnonymousStringMarker.Should().Be("CUSTOM");
    }

    [Fact]
    public void AddRetentionExecutor_WithoutConfigure_UsesDefaults()
    {
        var services = new ServiceCollection();
        services.AddRetentionExecutor();

        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<RetentionExecutorOptions>();
        options.AnonymousStringMarker.Should().Be("[ANONYMIZED]");
    }

    [Fact]
    public void AddRetentionExecutor_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddRetentionExecutor();
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
