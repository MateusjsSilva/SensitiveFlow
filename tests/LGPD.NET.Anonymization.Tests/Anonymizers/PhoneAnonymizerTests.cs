using FluentAssertions;
using LGPD.NET.Anonymization.Anonymizers;

namespace LGPD.NET.Anonymization.Tests.Anonymizers;

public sealed class PhoneAnonymizerTests
{
    private readonly PhoneAnonymizer _sut = new();

    [Theory]
    [InlineData("(11) 99999-8877")]
    [InlineData("+55 11 99999-8877")]
    [InlineData("1199998877")]
    public void CanAnonymize_ValidPhones_ReturnsTrue(string value)
    {
        _sut.CanAnonymize(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void CanAnonymize_InvalidPhones_ReturnsFalse(string value)
    {
        _sut.CanAnonymize(value).Should().BeFalse();
    }

    [Fact]
    public void Anonymize_KeepsLastTwoDigits()
    {
        var result = _sut.Anonymize("(11) 99999-8877");

        result.Should().EndWith("77");
    }

    [Fact]
    public void Anonymize_MasksLeadingDigits()
    {
        var result = _sut.Anonymize("(11) 99999-8877");

        result.Should().NotContain("11").And.NotContain("99999").And.NotContain("88");
    }
}
