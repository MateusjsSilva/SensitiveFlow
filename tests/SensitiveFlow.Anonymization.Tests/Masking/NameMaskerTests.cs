using FluentAssertions;
using SensitiveFlow.Anonymization.Masking;

namespace SensitiveFlow.Anonymization.Tests.Masking;

public sealed class NameMaskerTests
{
    private readonly NameMasker _sut = new();

    [Fact]
    public void CanMask_NonEmptyString_ReturnsTrue()
    {
        _sut.CanMask("João Silva").Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CanMask_EmptyOrWhitespace_ReturnsFalse(string value)
    {
        _sut.CanMask(value).Should().BeFalse();
    }

    [Fact]
    public void Mask_FullName_KeepsFirstLetterOfEachWord()
    {
        _sut.Mask("João da Silva").Should().Be("J*** d* S****");
    }

    [Fact]
    public void Mask_SingleWord_KeepsFirstLetter()
    {
        _sut.Mask("Carlos").Should().Be("C*****");
    }

    [Fact]
    public void Mask_SingleChar_MasksEntirely()
    {
        _sut.Mask("J").Should().Be("*");
    }

    [Fact]
    public void Mask_DoesNotRevealFullName()
    {
        var result = _sut.Mask("Maria Oliveira");

        result.Should().NotContain("aria").And.NotContain("liveira");
    }

    [Fact]
    public void Mask_UnrecognizedValue_ReturnsOriginal()
    {
        _sut.Mask("   ").Should().Be("   ");
    }
}
