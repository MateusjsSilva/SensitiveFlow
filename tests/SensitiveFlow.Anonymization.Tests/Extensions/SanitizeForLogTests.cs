using FluentAssertions;
using SensitiveFlow.Anonymization.Extensions;

namespace SensitiveFlow.Anonymization.Tests.Extensions;

public sealed class SanitizeForLogTests
{
    [Fact]
    public void SanitizeForLog_RemovesCarriageReturn()
    {
        var result = "hello\rworld".SanitizeForLog();
        result.Should().Be("helloworld");
    }

    [Fact]
    public void SanitizeForLog_RemovesLineFeed()
    {
        var result = "hello\nworld".SanitizeForLog();
        result.Should().Be("helloworld");
    }

    [Fact]
    public void SanitizeForLog_RemovesBothCrAndLf()
    {
        var result = "a\r\nb\nc\r".SanitizeForLog();
        result.Should().Be("abc");
    }

    [Fact]
    public void SanitizeForLog_Null_ReturnsEmpty()
    {
        string? input = null;
        var result = input.SanitizeForLog();
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeForLog_Empty_ReturnsEmpty()
    {
        var result = "".SanitizeForLog();
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeForLog_CleanString_Unchanged()
    {
        var result = "clean-string_123".SanitizeForLog();
        result.Should().Be("clean-string_123");
    }

    [Fact]
    public void SanitizeForLog_PreventsLogForging()
    {
        var malicious = "user\n[ERROR] Fake alert\n[INFO] Compromised";
        var result = malicious.SanitizeForLog();
        result.Should().NotContain("\n").And.NotContain("\r");
    }
}
