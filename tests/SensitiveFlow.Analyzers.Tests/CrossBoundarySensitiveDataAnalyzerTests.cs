using FluentAssertions;
using Microsoft.CodeAnalysis;
using SensitiveFlow.Analyzers.Analyzers;

namespace SensitiveFlow.Analyzers.Tests;

public sealed class CrossBoundarySensitiveDataAnalyzerTests
{
    private readonly CrossBoundarySensitiveDataAnalyzer _analyzer = new();

    [Fact]
    public void SupportedDiagnostics_ContainsCrossBoundaryRule()
    {
        var diagnostics = _analyzer.SupportedDiagnostics;

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("SF0005");
    }

    [Fact]
    public async Task Analyzer_WarnsWhenSensitiveDataReturnedWithoutAuthorize()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

[ApiController]
public sealed class CustomersController
{
    [HttpGet]
    public CustomerResponse GetCustomer(string id)
    {
        return new CustomerResponse { Email = "alice@example.com" };
    }
}

public sealed class CustomerResponse
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class ApiControllerAttribute : System.Attribute {}
public sealed class HttpGetAttribute : System.Attribute {}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().Satisfy(d =>
            d.Id == "SF0005" &&
            d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task Analyzer_DoesNotWarnWhenAuthorizePresent()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

[ApiController]
public sealed class CustomersController
{
    [Authorize]
    [HttpGet]
    public CustomerResponse GetCustomer(string id)
    {
        return new CustomerResponse { Email = "alice@example.com" };
    }
}

public sealed class CustomerResponse
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class ApiControllerAttribute : System.Attribute {}
public sealed class HttpGetAttribute : System.Attribute {}
public sealed class AuthorizeAttribute : System.Attribute {}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().NotContain(d => d.Id == "SF0005");
    }

    [Fact]
    public async Task Analyzer_DoesNotWarnForPublicNonSensitiveFields()
    {
        const string source = """
[ApiController]
public sealed class ProductsController
{
    [HttpGet]
    public ProductResponse GetProduct(string id)
    {
        return new ProductResponse { Name = "Widget" };
    }
}

public sealed class ProductResponse
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ApiControllerAttribute : System.Attribute {}
public sealed class HttpGetAttribute : System.Attribute {}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().NotContain(d => d.Id == "SF0005");
    }

    [Fact]
    public async Task Analyzer_DoesNotWarnForClassLevelAuthorize()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

[ApiController]
[Authorize]
public sealed class CustomersController
{
    [HttpGet]
    public CustomerResponse GetCustomer(string id)
    {
        return new CustomerResponse { Email = "alice@example.com" };
    }
}

public sealed class CustomerResponse
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class ApiControllerAttribute : System.Attribute {}
public sealed class HttpGetAttribute : System.Attribute {}
public sealed class AuthorizeAttribute : System.Attribute {}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().NotContain(d => d.Id == "SF0005");
    }

    [Fact]
    public async Task Analyzer_DoesNotWarnForNonHttpMethods()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class DataProcessor
{
    public CustomerResponse GetCustomer(string id)
    {
        return new CustomerResponse { Email = "alice@example.com" };
    }
}

public sealed class CustomerResponse
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().NotContain(d => d.Id == "SF0005");
    }
}
