using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SensitiveFlow.Logging.Loggers;
using SensitiveFlow.Logging.Redaction;

namespace SensitiveFlow.Logging.Tests;

public sealed class RedactingLoggerTests
{
    private static (RedactingLogger, ILogger) MakeLogger(string marker = "[REDACTED]")
    {
        var inner = Substitute.For<ILogger>();
        inner.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var redactor = new DefaultSensitiveValueRedactor(marker);
        return (new RedactingLogger(inner, redactor), inner);
    }

    // Tests for the redactor itself — isolated from the logger decorator.

    [Fact]
    public void DefaultRedactor_NoSensitiveMarker_ReturnsOriginal()
    {
        var redactor = new DefaultSensitiveValueRedactor();
        redactor.Redact("Hello World").Should().Be("[REDACTED]");
    }

    [Fact]
    public void DefaultRedactor_CustomMarker()
    {
        var redactor = new DefaultSensitiveValueRedactor("***");
        redactor.Redact("anything").Should().Be("***");
    }

    [Fact]
    public void DefaultRedactor_EmptyMarker_Throws()
    {
        var act = () => new DefaultSensitiveValueRedactor("   ");
        act.Should().Throw<ArgumentException>();
    }

    // Tests for the logger decorator.

    [Fact]
    public void IsEnabled_DelegatesToInner()
    {
        var (logger, inner) = MakeLogger();
        inner.IsEnabled(LogLevel.Warning).Returns(false);
        logger.IsEnabled(LogLevel.Warning).Should().BeFalse();
    }

    [Fact]
    public void Log_WhenEnabled_ForwardsToInner()
    {
        var (logger, inner) = MakeLogger();

        logger.Log(LogLevel.Information, new EventId(1), "message", null, (s, _) => s);

        inner.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<string>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<string, Exception?, string>>());
    }

    [Fact]
    public void Log_Disabled_DoesNotForwardToInner()
    {
        var (logger, inner) = MakeLogger();
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
        var (logger, inner) = MakeLogger();
        logger.BeginScope("scope");
        inner.Received(1).BeginScope("scope");
    }
}

public sealed class RedactionPatternTests
{
    [Theory]
    [InlineData("Hello World", "Hello World")]
    [InlineData("User [Sensitive]Email logged in", "User [REDACTED] logged in")]
    [InlineData("[Sensitive]Email and [Sensitive]Phone", "[REDACTED] and [REDACTED]")]
    [InlineData("no sensitive here", "no sensitive here")]
    public void RedactingLoggerProvider_RedactsPattern(string input, string expected)
    {
        var inner = Substitute.For<ILogger>();
        inner.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        string? capturedMessage = null;
        inner.When(l => l.Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Any<string>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<string, Exception?, string>>()))
            .Do(ci =>
            {
                var formatter = (Func<string, Exception?, string>)ci[4];
                capturedMessage = formatter((string)ci[2], (Exception?)ci[3]);
            });

        var logger = new RedactingLogger(inner, new DefaultSensitiveValueRedactor());

        logger.Log(LogLevel.Information, new EventId(0), input, null, (s, _) => s);

        capturedMessage.Should().Be(expected);
    }
}
