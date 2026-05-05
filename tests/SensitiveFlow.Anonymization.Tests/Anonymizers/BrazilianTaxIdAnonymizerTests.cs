using FluentAssertions;
using SensitiveFlow.Anonymization.Anonymizers;

namespace SensitiveFlow.Anonymization.Tests.Anonymizers;

public sealed class BrazilianTaxIdAnonymizerTests
{
    private readonly BrazilianTaxIdAnonymizer _sut = new();

    [Theory]
    [InlineData("123.456.789-09")]
    [InlineData("12.345.678/0001-95")]
    [InlineData("12345678909")]
    [InlineData("12345678000195")]
    public void CanAnonymize_ValidFormats_ReturnsTrue(string value)
    {
        _sut.CanAnonymize(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-tax-id")]
    public void CanAnonymize_InvalidFormats_ReturnsFalse(string value)
    {
        _sut.CanAnonymize(value).Should().BeFalse();
    }

    [Fact]
    public void Anonymize_Cpf_MasksAllDigitsPreservesPunctuation()
    {
        var result = _sut.Anonymize("123.456.789-09");

        result.Should().Be("***.***.***-**");
        result.Should().NotContain("1").And.NotContain("2").And.NotContain("3");
    }

    [Fact]
    public void Anonymize_Cnpj_MasksAllDigitsPreservesPunctuation()
    {
        var result = _sut.Anonymize("12.345.678/0001-95");

        result.Should().Be("**.***.***/" + "****-**");
    }

    [Fact]
    public void Anonymize_UnrecognizedValue_ReturnsOriginal()
    {
        var result = _sut.Anonymize("not-a-tax-id");

        result.Should().Be("not-a-tax-id");
    }

    [Fact]
    public void Anonymize_IsIrreversible_OriginalCannotBeRecovered()
    {
        var original = "123.456.789-09";
        var anonymized = _sut.Anonymize(original);

        anonymized.Should().NotBe(original);
        // No way to recover original from masked value
        anonymized.Should().NotContain("123");
    }
}
