using FluentAssertions;
using LGPD.NET.Anonymization.Masking;

namespace LGPD.NET.Anonymization.Tests.Masking;

public sealed class EmailMaskerTests
{
    private readonly EmailMasker _sut = new();

    [Theory]
    [InlineData("joao@example.com")]
    [InlineData("a@b.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    public void CanMask_ValidEmails_ReturnsTrue(string value)
    {
        _sut.CanMask(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("user@evil@example.com")]
    [InlineData("@nodomain")]
    public void CanMask_InvalidEmails_ReturnsFalse(string value)
    {
        _sut.CanMask(value).Should().BeFalse();
    }

    [Fact]
    public void Mask_LongLocalPart_KeepsFirstLetterMasksRest()
    {
        _sut.Mask("joao.silva@example.com").Should().Be("j*********@example.com");
    }

    [Fact]
    public void Mask_SingleCharLocal_MasksEntirely()
    {
        _sut.Mask("a@example.com").Should().Be("*@example.com");
    }

    [Fact]
    public void Mask_PreservesDomainIntact()
    {
        _sut.Mask("user@company.com.br").Should().EndWith("@company.com.br");
    }

    [Fact]
    public void Mask_UnrecognizedValue_ReturnsOriginal()
    {
        _sut.Mask("notanemail").Should().Be("notanemail");
    }

    [Fact]
    public void Mask_MultipleAtSigns_ReturnsOriginal()
    {
        _sut.Mask("user@evil@example.com").Should().Be("user@evil@example.com");
    }
}
