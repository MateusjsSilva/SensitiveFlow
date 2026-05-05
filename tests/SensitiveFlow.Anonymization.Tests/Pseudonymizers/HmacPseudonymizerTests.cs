using FluentAssertions;
using SensitiveFlow.Anonymization.Pseudonymizers;

namespace SensitiveFlow.Anonymization.Tests.Pseudonymizers;

public sealed class HmacPseudonymizerTests
{
    private const string SecretKey = "test-secret-key-32-bytes-minimum!";
    private readonly HmacPseudonymizer _sut = new(SecretKey);

    [Fact]
    public void Pseudonymize_ReturnsDifferentValueFromOriginal()
    {
        var token = _sut.Pseudonymize("joao@example.com");

        token.Should().NotBe("joao@example.com");
    }

    [Fact]
    public void Pseudonymize_SameInputSameKey_ProducesSameToken()
    {
        var token1 = _sut.Pseudonymize("joao@example.com");
        var token2 = _sut.Pseudonymize("joao@example.com");

        token1.Should().Be(token2);
    }

    [Fact]
    public void Pseudonymize_DifferentKeys_ProduceDifferentTokens()
    {
        var sut2 = new HmacPseudonymizer("different-secret-key-32-bytes!!!!");

        var token1 = _sut.Pseudonymize("joao@example.com");
        var token2 = sut2.Pseudonymize("joao@example.com");

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void Pseudonymize_DifferentInputs_ProduceDifferentTokens()
    {
        var token1 = _sut.Pseudonymize("value-a");
        var token2 = _sut.Pseudonymize("value-b");

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void Pseudonymize_ReturnsHexString()
    {
        var token = _sut.Pseudonymize("any-value");

        token.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Reverse_ThrowsNotSupportedException()
    {
        var token = _sut.Pseudonymize("value");

        var act = () => _sut.Reverse(token);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Constructor_ShortKey_ThrowsArgumentException()
    {
        var act = () => new HmacPseudonymizer("short-key");

        act.Should().Throw<ArgumentException>().WithMessage("*32*");
    }
}
