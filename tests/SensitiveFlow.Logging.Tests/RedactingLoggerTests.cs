using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SensitiveFlow.Logging.Loggers;
using SensitiveFlow.Logging.Redaction;

namespace SensitiveFlow.Logging.Tests;

public sealed class RedactingLoggerTests
{
    private static RedactingLogger MakeLogger(out ILogger inner, string marker = "[REDACTED]")
    {
        inner = Substitute.For<ILogger>();
        inner.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var redactor = new DefaultSensitiveValueRedactor(marker);
        return new RedactingLogger(inner, redactor);
    }

    [Fact]
    public void RedactMessage_NoSensitiveMarker_ReturnsOriginal()
    {
        var logger = MakeLogger(out _);
        logger.RedactMessage("Hello World").Should().Be("Hello World");
    }

    [Fact]
    public void RedactMessage_SensitiveMarker_Redacts()
    {
        var logger = MakeLogger(out _);
        var result = logger.RedactMessage("User [Sensitive]Email logged in");
        result.Should().Be("User [REDACTED] logged in");
    }

    [Fact]
    public void RedactMessage_MultipleSensitiveMarkers_AllRedacted()
    {
        var logger = MakeLogger(out _);
        var result = logger.RedactMessage("[Sensitive]Email and [Sensitive]Phone");
        result.Should().Be("[REDACTED] and [REDACTED]");
    }

    [Fact]
    public void RedactMessage_CustomMarker_UsesCustom()
    {
        var logger = MakeLogger(out _, marker: "***");
        var result = logger.RedactMessage("[Sensitive]CPF value");
        result.Should().Be("*** value");
    }

    [Fact]
    public void IsEnabled_DelegatesToInner()
    {
        var logger = MakeLogger(out var inner);
        inner.IsEnabled(LogLevel.Warning).Returns(false);

        logger.IsEnabled(LogLevel.Warning).Should().BeFalse();
    }

    [Fact]
    public void Log_Disabled_DoesNotForwardToInner()
    {
        var logger = MakeLogger(out var inner);
        inner.IsEnabled(LogLevel.Debug).Returns(false);

        logger.Log(LogLevel.Debug, new EventId(1), "state", null, (s, _) => s);

        inner.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<string, Exception?, string>>());
    }

    [Fact]
    public void BeginScope_DelegatesToInner()
    {
        var logger = MakeLogger(out var inner);
        logger.BeginScope("scope");
        inner.Received(1).BeginScope("scope");
    }
}
