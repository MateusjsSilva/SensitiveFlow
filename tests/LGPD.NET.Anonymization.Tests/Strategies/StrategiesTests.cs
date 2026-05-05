using FluentAssertions;
using LGPD.NET.Anonymization.Strategies;

namespace LGPD.NET.Anonymization.Tests.Strategies;

public sealed class StrategiesTests
{
    [Fact]
    public void RedactionStrategy_ReplacesWithDefaultMarker()
    {
        var result = new RedactionStrategy().Apply("sensitive");

        result.Should().Be("[REDACTED]");
    }

    [Fact]
    public void RedactionStrategy_CustomMarker_UsesCustomText()
    {
        var result = new RedactionStrategy("***").Apply("sensitive");

        result.Should().Be("***");
    }

    [Fact]
    public void HashStrategy_ProducesHexString()
    {
        var result = new HashStrategy().Apply("value");

        result.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void HashStrategy_SameInput_ProducesSameHash()
    {
        var h1 = new HashStrategy().Apply("value");
        var h2 = new HashStrategy().Apply("value");

        h1.Should().Be(h2);
    }

    [Fact]
    public void HashStrategy_WithSalt_DifferentFromWithoutSalt()
    {
        var withoutSalt = new HashStrategy().Apply("value");
        var withSalt    = new HashStrategy("my-salt-sixteen!").Apply("value");

        withSalt.Should().NotBe(withoutSalt);
    }

    [Fact]
    public void HashStrategy_ShortSalt_ThrowsArgumentException()
    {
        var act = () => new HashStrategy("short");

        act.Should().Throw<ArgumentException>().WithMessage("*16*");
    }

    [Fact]
    public void RedactionStrategy_MarkerExceeds200Chars_ThrowsArgumentException()
    {
        var longMarker = new string('x', 201);
        var act = () => new RedactionStrategy(longMarker);

        act.Should().Throw<ArgumentException>().WithMessage("*200*");
    }

    [Fact]
    public void HashStrategy_DifferentInputs_ProduceDifferentHashes()
    {
        var h1 = new HashStrategy().Apply("value-a");
        var h2 = new HashStrategy().Apply("value-b");

        h1.Should().NotBe(h2);
    }
}
