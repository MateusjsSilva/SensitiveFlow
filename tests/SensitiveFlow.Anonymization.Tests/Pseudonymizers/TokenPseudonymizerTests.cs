using FluentAssertions;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Anonymization.Tests.Stores;

namespace SensitiveFlow.Anonymization.Tests.Pseudonymizers;

#pragma warning disable CS0618 // Type or member is obsolete
public sealed class TokenPseudonymizerTests
{
    private static TokenPseudonymizer Create() => new(new InMemoryTokenStore());

    [Fact]
    public void Pseudonymize_ReturnsDifferentValueFromOriginal()
    {
        var token = Create().Pseudonymize("123.456.789-09");

        token.Should().NotBe("123.456.789-09");
    }

    [Fact]
    public void Pseudonymize_SameInputProducesSameToken()
    {
        var sut = Create();

        var token1 = sut.Pseudonymize("joao@example.com");
        var token2 = sut.Pseudonymize("joao@example.com");

        token1.Should().Be(token2);
    }

    [Fact]
    public void Pseudonymize_DifferentInputsProduceDifferentTokens()
    {
        var sut = Create();

        var token1 = sut.Pseudonymize("value-a");
        var token2 = sut.Pseudonymize("value-b");

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void Reverse_ReturnsOriginalValue()
    {
        var sut = Create();
        var original = "123.456.789-09";
        var token = sut.Pseudonymize(original);

        var reversed = sut.Reverse(token);

        reversed.Should().Be(original);
    }

    [Fact]
    public void Reverse_UnknownToken_ThrowsKeyNotFoundException()
    {
        var act = () => Create().Reverse("non-existent-token");

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public async Task PseudonymizeAsync_AndReverseAsync_RoundTrip()
    {
        var sut = Create();
        var original = "sensitive-value";

        var token    = await sut.PseudonymizeAsync(original);
        var reversed = await sut.ReverseAsync(token);

        reversed.Should().Be(original);
    }

    [Fact]
    public void Pseudonymize_IsReversible_DistinguishesFromAnonymization()
    {
        // Documents the Art. 12 distinction: pseudonymized data remains personal
        // because the mapping can be reversed with the correct store.
        var sut = Create();
        var original = "sensitive-value";
        var token    = sut.Pseudonymize(original);
        var recovered = sut.Reverse(token);

        recovered.Should().Be(original);
    }

    [Fact]
    public void Pseudonymize_NewStoreInstance_LosesMapping()
    {
        // Documents why InMemoryTokenStore must NOT be used in production:
        // a new instance has no knowledge of tokens created by a previous instance.
        var store1 = new InMemoryTokenStore();
        var token  = new TokenPseudonymizer(store1).Pseudonymize("value");

        var store2 = new InMemoryTokenStore();
        var act    = () => new TokenPseudonymizer(store2).Reverse(token);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public async Task InMemoryTokenStore_ConcurrentAccess_BidirectionalMappingIsConsistent()
    {
        // Verifies that parallel calls never produce a token that cannot be reversed —
        // the previous ConcurrentDictionary implementation had a race between the two dictionaries.
        var store = new InMemoryTokenStore();
        const int threads = 50;
        const string value = "shared-value";

        var tasks  = Enumerable.Range(0, threads).Select(_ => store.GetOrCreateTokenAsync(value));
        var tokens = await Task.WhenAll(tasks);

        // All concurrent calls must return the same stable token.
        tokens.Should().OnlyContain(t => t == tokens[0]);

        // The token must be reversible — both dictionaries must be in sync.
        var resolved = await store.ResolveTokenAsync(tokens[0]);
        resolved.Should().Be(value);
    }
}
#pragma warning restore CS0618 // Type or member is obsolete
