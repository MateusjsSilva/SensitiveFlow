using FluentAssertions;
using LGPD.NET.Anonymization.Masking;

namespace LGPD.NET.Anonymization.Tests.Masking;

public sealed class PhoneMaskerTests
{
    private readonly PhoneMasker _sut = new();

    [Theory]
    [InlineData("(11) 99999-8877")]
    [InlineData("+55 11 99999-8877")]
    [InlineData("1199998877")]
    public void CanMask_ValidPhones_ReturnsTrue(string value)
    {
        _sut.CanMask(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("       ")]   // 7 spaces — no digits
    [InlineData("(((((((")]   // 7 parens — no digits
    public void CanMask_InvalidPhones_ReturnsFalse(string value)
    {
        _sut.CanMask(value).Should().BeFalse();
    }

    [Fact]
    public void Mask_KeepsLastTwoDigits()
    {
        _sut.Mask("(11) 99999-8877").Should().EndWith("77");
    }

    [Fact]
    public void Mask_MasksLeadingDigits()
    {
        var result = _sut.Mask("(11) 99999-8877");

        result.Should().NotContain("11").And.NotContain("99999").And.NotContain("88");
    }

    [Fact]
    public void Mask_UnrecognizedValue_ReturnsOriginal()
    {
        _sut.Mask("abc").Should().Be("abc");
    }
}
