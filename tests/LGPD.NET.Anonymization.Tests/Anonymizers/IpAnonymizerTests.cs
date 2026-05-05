using FluentAssertions;
using LGPD.NET.Anonymization.Anonymizers;

namespace LGPD.NET.Anonymization.Tests.Anonymizers;

public sealed class IpAnonymizerTests
{
    private readonly IpAnonymizer _sut = new();

    [Theory]
    [InlineData("192.168.1.10")]
    [InlineData("10.0.0.1")]
    [InlineData("2001:db8::1")]
    public void CanAnonymize_ValidIps_ReturnsTrue(string value)
    {
        _sut.CanAnonymize(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("notanip")]
    [InlineData("999.999.999.999")]
    public void CanAnonymize_InvalidIps_ReturnsFalse(string value)
    {
        _sut.CanAnonymize(value).Should().BeFalse();
    }

    [Fact]
    public void Anonymize_IPv4_ZerosLastOctet()
    {
        var result = _sut.Anonymize("192.168.1.10");

        result.Should().Be("192.168.1.0");
    }

    [Fact]
    public void Anonymize_IPv4_PreservesFirstThreeOctets()
    {
        var result = _sut.Anonymize("10.20.30.99");

        result.Should().StartWith("10.20.30.");
    }

    [Fact]
    public void Anonymize_InvalidValue_ReturnsOriginal()
    {
        var result = _sut.Anonymize("notanip");

        result.Should().Be("notanip");
    }
}
