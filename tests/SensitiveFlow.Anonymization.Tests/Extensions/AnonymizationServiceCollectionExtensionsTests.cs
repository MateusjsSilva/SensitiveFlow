using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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
    public void AddTokenStore_RegistersBothAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddTokenStore<FakeTokenStore>();

        var provider = services.BuildServiceProvider();

        var store1 = provider.GetRequiredService<ITokenStore>();
        var store2 = provider.GetRequiredService<ITokenStore>();
        store1.Should().BeSameAs(store2);

        var pseudo1 = provider.GetRequiredService<IPseudonymizer>();
        var pseudo2 = provider.GetRequiredService<IPseudonymizer>();
        pseudo1.Should().BeSameAs(pseudo2);
    }

    [Fact]
    public void AddTokenStore_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddTokenStore<FakeTokenStore>();
        result.Should().BeSameAs(services);
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid().ToString());

        public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(token);
    }
}
