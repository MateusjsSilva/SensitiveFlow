using FluentAssertions;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Anonymization.Tests.Stores;

namespace SensitiveFlow.Anonymization.Tests.Pseudonymizers;

public sealed class CanPseudonymizeTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void HmacPseudonymizer_CanPseudonymize_EmptyOrNull_ReturnsFalse(string? value)
    {
        var sut = new HmacPseudonymizer("my-secret-key-for-hmac-32-bytes!!");
        sut.CanPseudonymize(value!).Should().BeFalse();
    }

    [Fact]
    public void HmacPseudonymizer_CanPseudonymize_NonEmpty_ReturnsTrue()
    {
        var sut = new HmacPseudonymizer("my-secret-key-for-hmac-32-bytes!!");
        sut.CanPseudonymize("any-value").Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void TokenPseudonymizer_CanPseudonymize_EmptyOrNull_ReturnsFalse(string? value)
    {
        var sut = new TokenPseudonymizer(new InMemoryTokenStore());
        sut.CanPseudonymize(value!).Should().BeFalse();
    }

    [Fact]
    public void TokenPseudonymizer_CanPseudonymize_NonEmpty_ReturnsTrue()
    {
        var sut = new TokenPseudonymizer(new InMemoryTokenStore());
        sut.CanPseudonymize("any-value").Should().BeTrue();
    }
}
