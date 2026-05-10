using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SensitiveFlow.Anonymization.Decorators;
using SensitiveFlow.Anonymization.Erasure;
using SensitiveFlow.Anonymization.Export;
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
    public void AddTokenPseudonymizer_RegistersTokenPseudonymizerAsIPseudonymizer()
    {
        var services = new ServiceCollection();
        services.AddTokenStore<FakeTokenStore>();
        services.AddTokenPseudonymizer();

        var provider = services.BuildServiceProvider();

        provider.GetService<IPseudonymizer>().Should().BeOfType<TokenPseudonymizer>();
    }

    [Fact]
    public void AddPseudonymizer_RegistersCustomPseudonymizer()
    {
        var services = new ServiceCollection();
        services.AddPseudonymizer<FakePseudonymizer>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IPseudonymizer>().Should().BeOfType<FakePseudonymizer>();
    }

    [Fact]
    public void AddTokenStore_DoesNotRegisterIPseudonymizer()
    {
        var services = new ServiceCollection();
        services.AddTokenStore<FakeTokenStore>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IPseudonymizer>().Should().BeNull();
    }

    [Fact]
    public void AddTokenStore_RegistersAsScoped()
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

    [Fact]
    public void AddCachingTokenStore_WithoutTokenStore_ThrowsHelpfulException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddCachingTokenStore();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddTokenStore<T>()*AddCachingTokenStore*");
    }

    [Fact]
    public void AddCachingTokenStore_WrapsFactoryRegistration()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITokenStore>(_ => new FakeTokenStore());

        services.AddCachingTokenStore();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITokenStore>()
            .Should().BeOfType<CachingTokenStore>();
    }

    [Fact]
    public void AddCachingTokenStore_WrapsInstanceRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITokenStore>(new FakeTokenStore());

        services.AddCachingTokenStore();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITokenStore>()
            .Should().BeOfType<CachingTokenStore>();
    }

    [Fact]
    public void AddDataSubjectErasure_RegistersDefaultServices()
    {
        var services = new ServiceCollection();

        var result = services.AddDataSubjectErasure();
        using var provider = services.BuildServiceProvider();

        result.Should().BeSameAs(services);
        provider.GetRequiredService<IErasureStrategy>().Should().BeOfType<RedactionErasureStrategy>();
        provider.GetRequiredService<IDataSubjectErasureService>().Should().BeOfType<DataSubjectErasureService>();
    }

    [Fact]
    public void AddDataSubjectErasure_DoesNotReplaceExistingStrategy()
    {
        var strategy = Substitute.For<IErasureStrategy>();
        var services = new ServiceCollection();
        services.AddSingleton(strategy);

        services.AddDataSubjectErasure();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IErasureStrategy>().Should().BeSameAs(strategy);
    }

    [Fact]
    public void AddDataSubjectExport_RegistersExporter()
    {
        var services = new ServiceCollection();

        var result = services.AddDataSubjectExport();
        using var provider = services.BuildServiceProvider();

        result.Should().BeSameAs(services);
        provider.GetRequiredService<IDataSubjectExporter>().Should().BeOfType<DataSubjectExporter>();
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid().ToString());

        public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(token);
    }

    private sealed class FakePseudonymizer : IPseudonymizer
    {
        public bool CanPseudonymize(string value) => true;
        public string Pseudonymize(string value) => value;
        public Task<string> PseudonymizeAsync(string value, CancellationToken cancellationToken = default)
            => Task.FromResult(value);
        public string Reverse(string token) => token;
        public Task<string> ReverseAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(token);
    }
}
