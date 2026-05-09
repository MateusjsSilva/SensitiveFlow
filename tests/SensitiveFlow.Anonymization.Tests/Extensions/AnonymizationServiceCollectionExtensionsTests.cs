using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SensitiveFlow.Anonymization.Decorators;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Tests.Extensions;

public sealed class AnonymizationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTokenStore_RegistersITokenStore()
    {
        var services = new ServiceCollection();
        services.AddTokenStore<FakeTokenStore>();

        var provider = services.BuildServiceProvider();

        provider.GetService<ITokenStore>().Should().BeOfType<FakeTokenStore>();
    }

    [Fact]
    public void AddTokenStore_RegistersTokenPseudonymizerAsIPseudonymizer()
    {
        var services = new ServiceCollection();
        services.AddTokenStore<FakeTokenStore>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IPseudonymizer>().Should().BeOfType<TokenPseudonymizer>();
    }

    [Fact]
    public void AddTokenStore_RegistersBothAsScoped()
    {
        var services = new ServiceCollection();
        services.AddTokenStore<FakeTokenStore>();

        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var store1 = scope1.ServiceProvider.GetRequiredService<ITokenStore>();
        var store2 = scope1.ServiceProvider.GetRequiredService<ITokenStore>();
        var store3 = scope2.ServiceProvider.GetRequiredService<ITokenStore>();
        store1.Should().BeSameAs(store2);
        store1.Should().NotBeSameAs(store3);

        var pseudo1 = scope1.ServiceProvider.GetRequiredService<IPseudonymizer>();
        var pseudo2 = scope1.ServiceProvider.GetRequiredService<IPseudonymizer>();
        var pseudo3 = scope2.ServiceProvider.GetRequiredService<IPseudonymizer>();
        pseudo1.Should().BeSameAs(pseudo2);
        pseudo1.Should().NotBeSameAs(pseudo3);
    }

    [Fact]
    public void AddTokenStore_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddTokenStore<FakeTokenStore>();
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddCachingTokenStore_WrapsRegisteredTokenStore()
    {
        var services = new ServiceCollection();
        services.AddTokenStore<FakeTokenStore>();
        services.AddCachingTokenStore(options => options.MaxEntries = 10);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITokenStore>()
            .Should().BeOfType<CachingTokenStore>();
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid().ToString());

        public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(token);
    }
}
