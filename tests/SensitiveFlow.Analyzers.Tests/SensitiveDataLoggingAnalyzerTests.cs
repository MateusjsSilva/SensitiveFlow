using FluentAssertions;
using SensitiveFlow.Analyzers.Analyzers;
using Xunit;

namespace SensitiveFlow.Analyzers.Tests;

public sealed class SensitiveDataLoggingAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnostic_WhenSensitiveMemberIsLoggedDirectly()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;
using Microsoft.Extensions.Logging;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class Sample
{
    public void Execute(ILogger logger, Customer customer)
    {
        logger.LogInformation("customer email {Email}", customer.Email);
    }
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}

namespace Microsoft.Extensions.Logging
{
    public interface ILogger { }

    public static class LoggerExtensions
    {
        public static void LogInformation(this ILogger logger, string template, params object[] args) { }
    }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataLoggingAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0001");
    }

    [Fact]
    public async Task DoesNotReport_WhenSensitiveMemberIsMasked()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;
using Microsoft.Extensions.Logging;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class Sample
{
    public void Execute(ILogger logger, Customer customer)
    {
        logger.LogInformation("customer email {Email}", customer.Email.Mask());
    }
}

public static class MaskingExtensions
{
    public static string Mask(this string value) => "***";
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}

namespace Microsoft.Extensions.Logging
{
    public interface ILogger { }

    public static class LoggerExtensions
    {
        public static void LogInformation(this ILogger logger, string template, params object[] args) { }
    }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataLoggingAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0001");
    }

    [Fact]
    public async Task ReportsDiagnostic_WhenSensitiveMemberIsLoggedWithGenericILogger()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;
using Microsoft.Extensions.Logging;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class Sample
{
    private readonly ILogger<Sample> _logger;

    public Sample(ILogger<Sample> logger)
    {
        _logger = logger;
    }

    public void Execute(Customer customer)
    {
        _logger.LogInformation("customer email {Email}", customer.Email);
    }
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}

namespace Microsoft.Extensions.Logging
{
    public interface ILogger { }

    public interface ILogger<T> : ILogger { }

    public static class LoggerExtensions
    {
        public static void LogInformation(this ILogger logger, string template, params object[] args) { }
    }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataLoggingAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0001");
    }

    [Fact]
    public async Task ReportsDiagnostic_WhenSensitiveMemberIsLoggedThroughGenericILoggerInterface()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;
using Microsoft.Extensions.Logging;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class Sample
{
    private readonly ILogger<Sample> _logger;

    public Sample(ILogger<Sample> logger)
    {
        _logger = logger;
    }

    public void Execute(Customer customer)
    {
        _logger.LogInformation("customer email {Email}", customer.Email);
    }
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}

namespace Microsoft.Extensions.Logging
{
    public interface ILogger { }

    public interface ILogger<T> : ILogger
    {
        void LogInformation(string template, params object[] args);
    }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataLoggingAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0001");
    }

    [Fact]
    public async Task ReportsDiagnostic_WhenLoggingTypeNameStartsWithILoggerInLoggingNamespace()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class Sample
{
    public void Execute(Customer customer)
    {
        Microsoft.Extensions.Logging.ILoggerDiagnostics.LogInformation(customer.Email);
    }
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}

namespace Microsoft.Extensions.Logging
{
    public static class ILoggerDiagnostics
    {
        public static void LogInformation(object value) { }
    }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataLoggingAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0001");
    }
}
