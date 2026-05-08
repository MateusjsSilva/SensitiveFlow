using FluentAssertions;
using SensitiveFlow.Core.Interfaces;
using Xunit;

namespace SensitiveFlow.TestKit;

/// <summary>
/// Conformance suite for <see cref="ITokenStore"/> implementations. Verifies idempotency,
/// reversibility, and concurrent-access invariants. Inherit and override
/// <see cref="CreateStoreAsync"/>.
/// </summary>
public abstract class TokenStoreContractTests
{
    /// <summary>Creates a fresh store instance for the test.</summary>
    protected abstract Task<ITokenStore> CreateStoreAsync();

    /// <summary>Cleanup hook for stores that need teardown.</summary>
    protected virtual Task DisposeStoreAsync(ITokenStore store) => Task.CompletedTask;

    [Fact]
    public async Task GetOrCreate_SameValue_ReturnsStableToken()
    {
        var store = await CreateStoreAsync();
        try
        {
            var t1 = await store.GetOrCreateTokenAsync("subject-x");
            var t2 = await store.GetOrCreateTokenAsync("subject-x");

            t1.Should().Be(t2);
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    [Fact]
    public async Task GetOrCreate_DistinctValues_ProduceDistinctTokens()
    {
        var store = await CreateStoreAsync();
        try
        {
            var t1 = await store.GetOrCreateTokenAsync("subject-x");
            var t2 = await store.GetOrCreateTokenAsync("subject-y");

            t1.Should().NotBe(t2);
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    [Fact]
    public async Task ResolveToken_ReturnsOriginalValue()
    {
        var store = await CreateStoreAsync();
        try
        {
            var token = await store.GetOrCreateTokenAsync("original");
            var resolved = await store.ResolveTokenAsync(token);

            resolved.Should().Be("original");
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    [Fact]
    public async Task ResolveToken_UnknownToken_Throws()
    {
        var store = await CreateStoreAsync();
        try
        {
            await store.Invoking(s => s.ResolveTokenAsync("unknown-token"))
                .Should().ThrowAsync<KeyNotFoundException>();
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }

    [Fact]
    public async Task GetOrCreate_ConcurrentCallsForSameValue_ReturnSameToken()
    {
        var store = await CreateStoreAsync();
        try
        {
            const int parallelism = 16;
            var tasks = Enumerable.Range(0, parallelism)
                .Select(_ => store.GetOrCreateTokenAsync("shared-value"));
            var tokens = await Task.WhenAll(tasks);

            tokens.Should().OnlyContain(t => t == tokens[0]);

            var resolved = await store.ResolveTokenAsync(tokens[0]);
            resolved.Should().Be("shared-value");
        }
        finally
        {
            await DisposeStoreAsync(store);
        }
    }
}
