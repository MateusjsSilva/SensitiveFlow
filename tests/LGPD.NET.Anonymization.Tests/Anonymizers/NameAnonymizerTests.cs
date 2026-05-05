using FluentAssertions;
using LGPD.NET.Anonymization.Anonymizers;

namespace LGPD.NET.Anonymization.Tests.Anonymizers;

public sealed class NameAnonymizerTests
{
    private readonly NameAnonymizer _sut = new();

    [Fact]
    public void CanAnonymize_NonEmptyString_ReturnsTrue()
    {
        _sut.CanAnonymize("João Silva").Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CanAnonymize_EmptyOrWhitespace_ReturnsFalse(string value)
    {
        _sut.CanAnonymize(value).Should().BeFalse();
    }

    [Fact]
    public void Anonymize_FullName_KeepsFirstLetterOfEachWord()
    {
        var result = _sut.Anonymize("João da Silva");

        result.Should().Be("J*** d* S****");
    }

    [Fact]
    public void Anonymize_SingleWord_KeepsFirstLetter()
    {
        var result = _sut.Anonymize("Carlos");

        result.Should().Be("C*****");
    }

    [Fact]
    public void Anonymize_SingleChar_MasksEntirely()
    {
        var result = _sut.Anonymize("J");

        result.Should().Be("*");
    }

    [Fact]
    public void Anonymize_DoesNotRevealFullName()
    {
        var result = _sut.Anonymize("Maria Oliveira");

        result.Should().NotContain("aria").And.NotContain("liveira");
    }
}
