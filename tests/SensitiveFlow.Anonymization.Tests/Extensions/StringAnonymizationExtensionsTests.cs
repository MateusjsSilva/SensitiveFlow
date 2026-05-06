using FluentAssertions;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Anonymization.Tests.Stores;

namespace SensitiveFlow.Anonymization.Tests.Extensions;

public sealed class StringAnonymizationExtensionsTests
{
    [Theory]
    [InlineData("123.456.789-09", true)]
    [InlineData("12.345.678/0001-95", false)]
    public void AnonymizeTaxId_ReplacesDigitsWithAsterisks(string input, bool isCpf)
    {
        var result = input.AnonymizeTaxId();

        result.Should().NotContainAny("0", "1", "2", "3", "4", "5", "6", "7", "8", "9");
        _ = isCpf; // parameter documents the test data intent
    }

    [Fact]
    public void AnonymizeTaxId_InvalidValue_ReturnsOriginal()
    {
        var result = "not-a-taxid".AnonymizeTaxId();
        result.Should().Be("not-a-taxid");
    }

    [Fact]
    public void MaskEmail_MasksLocalPart()
    {
        var result = "joao.silva@example.com".MaskEmail();
        result.Should().StartWith("j").And.Contain("@example.com");
    }

    [Fact]
    public void MaskEmail_InvalidEmail_ReturnsOriginal()
    {
        var result = "not-an-email".MaskEmail();
        result.Should().Be("not-an-email");
    }

    [Fact]
    public void MaskPhone_HidesLeadingDigits()
    {
        var result = "(11) 99999-8877".MaskPhone();
        result.Should().EndWith("77").And.NotContain("11");
    }

    [Fact]
    public void MaskPhone_InvalidPhone_ReturnsOriginal()
    {
        var result = "abc".MaskPhone();
        result.Should().Be("abc");
    }

    [Fact]
    public void MaskName_KeepsFirstLetterOfEachWord()
    {
        var result = "Joao Silva".MaskName();
        result.Should().StartWith("J").And.Contain(" S");
    }

    [Fact]
    public void MaskName_InvalidName_ReturnsOriginal()
    {
        var result = "   ".MaskName();
        result.Should().Be("   ");
    }

    [Fact]
    public void Pseudonymize_WithTokenPseudonymizer_ReturnsDifferentValue()
    {
        var store = new InMemoryTokenStore();
        var pseudonymizer = new TokenPseudonymizer(store);

        var result = "joao@example.com".Pseudonymize(pseudonymizer);

        result.Should().NotBe("joao@example.com");
    }

    [Fact]
    public void PseudonymizeHmac_ReturnsDeterministicToken()
    {
        const string key = "my-secret-key-for-hmac-32-bytes!!";
        var result1 = "joao@example.com".PseudonymizeHmac(key);
        var result2 = "joao@example.com".PseudonymizeHmac(key);

        result1.Should().Be(result2).And.NotBe("joao@example.com");
    }
}
