using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Logging.Extensions;
using SensitiveFlow.Logging.Redaction;

namespace SensitiveFlow.Logging.Tests;

public sealed class LoggingExtensionsTests
{
    [Fact]
    public void AddSensitiveFlowLogging_RegistersDefaultRedactor()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowLogging();

        var provider = services.BuildServiceProvider();

        provider.GetService<ISensitiveValueRedactor>().Should().BeOfType<DefaultSensitiveValueRedactor>();
    }

    [Fact]
    public void AddSensitiveFlowLogging_RegistersAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowLogging();

        var provider = services.BuildServiceProvider();

        var a = provider.GetRequiredService<ISensitiveValueRedactor>();
        var b = provider.GetRequiredService<ISensitiveValueRedactor>();

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void AddSensitiveFlowLogging_DefaultMarker_IsRedacted()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowLogging();

        var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<ISensitiveValueRedactor>();

        redactor.Redact("anything").Should().Be("[REDACTED]");
    }

    [Fact]
    public void AddSensitiveFlowLogging_CustomMarker_UsedByRedactor()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowLogging("***");

        var provider = services.BuildServiceProvider();
        var redactor = provider.GetRequiredService<ISensitiveValueRedactor>();

        redactor.Redact("anything").Should().Be("***");
    }

    [Fact]
    public void AddSensitiveFlowLogging_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddSensitiveFlowLogging();
        result.Should().BeSameAs(services);
    }
}

public sealed class RedactingLoggerProviderTests
{
    [Fact]
    public void CreateLogger_ReturnsRedactingLogger()
    {
        var innerProvider = NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILoggerProvider>();
        innerProvider.CreateLogger(Arg.Any<string>()).Returns(NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger>());
        var redactor = new DefaultSensitiveValueRedactor();
        var provider = new SensitiveFlow.Logging.Loggers.RedactingLoggerProvider(innerProvider, redactor);

        var logger = provider.CreateLogger("TestCategory");

        logger.Should().BeOfType<SensitiveFlow.Logging.Loggers.RedactingLogger>();
    }

    [Fact]
    public void Dispose_DisposesInnerProvider()
    {
        var innerProvider = new DisposableProvider();
        var redactor = new DefaultSensitiveValueRedactor();
        var provider = new SensitiveFlow.Logging.Loggers.RedactingLoggerProvider(innerProvider, redactor);

        provider.Dispose();

        innerProvider.Disposed.Should().BeTrue();
    }

    private sealed class DisposableProvider : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public bool Disposed { get; private set; }

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
            => NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger>();

        public void Dispose() => Disposed = true;
    }
}
