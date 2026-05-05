using FluentAssertions;
using LGPD.NET.Anonymization.Anonymizers;

namespace LGPD.NET.Anonymization.Tests.Anonymizers;

public sealed class EmailAnonymizerTests
{
    private readonly EmailAnonymizer _sut = new();

    [Theory]
    [InlineData("joao@example.com")]
    [InlineData("a@b.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    public void CanAnonymize_ValidEmails_ReturnsTrue(string value)
    {
        _sut.CanAnonymize(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("user@evil@example.com")]
    [InlineData("@nodomain")]
    [InlineData("noatsign")]
    public void CanAnonymize_InvalidEmails_ReturnsFalse(string value)
    {
        _sut.CanAnonymize(value).Should().BeFalse();
    }

    [Fact]
    public void Anonymize_LongLocalPart_KeepsFirstLetterMasksRest()
    {
        var result = _sut.Anonymize("joao.silva@example.com");

        result.Should().Be("j*********@example.com");
    }

    [Fact]
    public void Anonymize_SingleCharLocal_MasksEntirely()
    {
        var result = _sut.Anonymize("a@example.com");

        result.Should().Be("*@example.com");
    }

    [Fact]
    public void Anonymize_PreservesDomainIntact()
    {
        var result = _sut.Anonymize("user@company.com.br");

        result.Should().EndWith("@company.com.br");
    }

    [Fact]
    public void Anonymize_UnrecognizedValue_ReturnsOriginal()
    {
        var result = _sut.Anonymize("notanemail");

        result.Should().Be("notanemail");
    }

    [Fact]
    public void Anonymize_MultipleAtSigns_ReturnsOriginal()
    {
        // Multiple @ makes it an invalid email — CanAnonymize returns false, value returned as-is.
        var result = _sut.Anonymize("user@evil@example.com");

        result.Should().Be("user@evil@example.com");
    }
}
