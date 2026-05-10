using FluentAssertions;
using NSubstitute;
using SensitiveFlow.Anonymization.Decorators;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Tests.Decorators;

public sealed class CachingTokenStoreTests
{
    [Fact]
    public async Task GetOrCreateTokenAsync_ReusesCachedToken()
    {
        var inner = Substitute.For<ITokenStore>();
        inner.GetOrCreateTokenAsync("value", Arg.Any<CancellationToken>())
            .Returns("token");
        var sut = new CachingTokenStore(inner);

        var first = await sut.GetOrCreateTokenAsync("value");
        var second = await sut.GetOrCreateTokenAsync("value");

        first.Should().Be("token");
        second.Should().Be("token");
        await inner.Received(1).GetOrCreateTokenAsync("value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveTokenAsync_ReusesTokenLearnedFromGetOrCreate()
    {
        var inner = Substitute.For<ITokenStore>();
        inner.GetOrCreateTokenAsync("value", Arg.Any<CancellationToken>())
            .Returns("token");
        var sut = new CachingTokenStore(inner);

        await sut.GetOrCreateTokenAsync("value");
        var resolved = await sut.ResolveTokenAsync("token");

        resolved.Should().Be("value");
        await inner.DidNotReceive().ResolveTokenAsync("token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveTokenAsync_CachesValueReturnedByInnerStore()
    {
        var inner = Substitute.For<ITokenStore>();
        inner.ResolveTokenAsync("token", Arg.Any<CancellationToken>())
            .Returns("value");
        var sut = new CachingTokenStore(inner);

        var first = await sut.ResolveTokenAsync("token");
        var second = await sut.ResolveTokenAsync("token");

        first.Should().Be("value");
        second.Should().Be("value");
        await inner.Received(1).ResolveTokenAsync("token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_RejectsNullInnerAndInvalidOptions()
    {
        var actNullInner = () => new CachingTokenStore(null!);
        var actInvalidOptions = () => new CachingTokenStore(Substitute.For<ITokenStore>(), new CachingTokenStoreOptions
        {
            MaxEntries = 0,
        });

        actNullInner.Should().Throw<ArgumentNullException>();
        actInvalidOptions.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetOrCreateTokenAsync_RejectsNullValue()
    {
        var sut = new CachingTokenStore(Substitute.For<ITokenStore>());

        await sut.Invoking(s => s.GetOrCreateTokenAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ResolveTokenAsync_RejectsNullToken()
    {
        var sut = new CachingTokenStore(Substitute.For<ITokenStore>());

        await sut.Invoking(s => s.ResolveTokenAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task MaxEntries_EvictsOldestMapping()
    {
        var inner = Substitute.For<ITokenStore>();
        inner.GetOrCreateTokenAsync("first", Arg.Any<CancellationToken>())
            .Returns("token-1");
        inner.GetOrCreateTokenAsync("second", Arg.Any<CancellationToken>())
            .Returns("token-2");
        var sut = new CachingTokenStore(inner, new CachingTokenStoreOptions
        {
            MaxEntries = 1,
        });

        await sut.GetOrCreateTokenAsync("first");
        await sut.GetOrCreateTokenAsync("second");
        await sut.GetOrCreateTokenAsync("first");

        await inner.Received(2).GetOrCreateTokenAsync("first", Arg.Any<CancellationToken>());
    }
}
