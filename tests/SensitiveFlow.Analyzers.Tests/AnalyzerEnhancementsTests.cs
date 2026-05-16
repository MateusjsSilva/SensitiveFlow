using FluentAssertions;
using SensitiveFlow.Analyzers.Analyzers;
using Xunit;

namespace SensitiveFlow.Analyzers.Tests;

public sealed class AnalyzerEnhancementsTests
{
    #region ILogger<T> Support Tests

    [Fact]
    public async Task SF0001_ReportsDiagnostic_WithGenericILoggerT()
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

    #endregion

    #region SensitiveFlowIgnoreAttribute Tests

    [Fact]
    public async Task SF0001_DoesNotReport_WhenPropertyHasSensitiveFlowIgnoreAttribute()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;
using Microsoft.Extensions.Logging;

public sealed class Customer
{
    [PersonalData]
    [SensitiveFlowIgnore]
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
    public sealed class SensitiveFlowIgnoreAttribute : System.Attribute { }
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
    public async Task SF0006_DoesNotReport_WhenPropertyHasSensitiveFlowIgnoreAttribute()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [PersonalData]
    [SensitiveFlowIgnore]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
    public sealed class SensitiveFlowIgnoreAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0006");
    }

    #endregion

    #region Cross-Assembly Tests

    [Fact]
    public async Task SF0001_ReportsWhenAttributeIsFromDifferentNamespace()
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
        logger.LogInformation("email {Email}", customer.Email);
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

    #endregion
}
