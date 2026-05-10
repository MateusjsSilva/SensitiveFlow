using FluentAssertions;
using SensitiveFlow.Analyzers.Analyzers;
using Xunit;

namespace SensitiveFlow.Analyzers.Tests;

public sealed class SensitiveDataResponseAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnostic_WhenSensitiveMemberIsReturnedByController()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class CustomersController : ControllerBase
{
    [HttpGet]
    public IActionResult Get(Customer customer)
    {
        return Ok(customer.Email);
    }
}

public abstract class ControllerBase
{
    protected IActionResult Ok(object? value) => default!;
}

public interface IActionResult {}

public sealed class HttpGetAttribute : System.Attribute {}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataResponseAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0002");
    }

    [Fact]
    public async Task DoesNotReport_WhenSensitiveMemberIsMaskedBeforeResponse()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [SensitiveData]
    public string TaxId { get; set; } = string.Empty;
}

public sealed class CustomersController : ControllerBase
{
    [HttpGet]
    public IActionResult Get(Customer customer)
    {
        return Ok(customer.TaxId.Mask());
    }
}

public static class MaskingExtensions
{
    public static string Mask(this string value) => "***";
}

public abstract class ControllerBase
{
    protected IActionResult Ok(object? value) => default!;
}

public interface IActionResult {}

public sealed class HttpGetAttribute : System.Attribute {}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class SensitiveDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataResponseAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0002");
    }

    [Fact]
    public async Task ReportsDiagnostic_WhenEndpointReturnsSensitivePropertyDirectly()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class CustomersController
{
    [Route]
    public string Get(Customer customer)
    {
        return customer.Email;
    }
}

public sealed class RouteAttribute : System.Attribute {}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataResponseAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0002");
    }

    [Fact]
    public async Task ReportsDiagnostic_WhenEndpointReturnsSensitiveFieldDirectly()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [SensitiveData]
    public string TaxId = string.Empty;
}

public sealed class CustomersController
{
    [Endpoint]
    public string Get(Customer customer)
    {
        return customer.TaxId;
    }
}

public sealed class EndpointAttribute : System.Attribute {}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataResponseAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0002");
    }

    [Fact]
    public async Task DoesNotReport_WhenEndpointReturnIsNullOrNonSensitive()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;

    public string PublicName { get; set; } = string.Empty;
}

public sealed class CustomersController
{
    [HttpPost]
    public string? Post(Customer customer)
    {
        return null;
    }

    [HttpGet]
    public string Get(Customer customer)
    {
        return customer.PublicName;
    }
}

public sealed class HttpGetAttribute : System.Attribute {}
public sealed class HttpPostAttribute : System.Attribute {}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataResponseAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0002");
    }

    [Fact]
    public async Task DoesNotReport_WhenSensitiveReturnIsNotHttpEndpoint()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class CustomerFormatter
{
    public string Format(Customer customer)
    {
        return customer.Email;
    }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataResponseAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0002");
    }

    [Fact]
    public async Task ReportsDiagnostic_ForResultsAndTypedResultsFactories()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class Endpoints
{
    public object GetJson(Customer customer)
    {
        return Results.Json(customer.Email);
    }

    public object GetCreated(Customer customer)
    {
        return TypedResults.Created("/customers/1", customer.Email);
    }
}

public static class Results
{
    public static object Json(object? value) => new();
}

public static class TypedResults
{
    public static object Created(string uri, object? value) => new();
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataResponseAnalyzer());

        diagnostics.Where(d => d.Id == "SF0002").Should().HaveCount(2);
    }

    [Fact]
    public async Task DoesNotReport_ForNonHttpFactoryNamesOrTypes()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class CustomersController : ControllerBase
{
    public object Get(Customer customer)
    {
        return NotFound(customer.Email);
    }
}

public abstract class ControllerBase
{
    protected object NotFound(object? value) => new();
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new SensitiveDataResponseAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0002");
    }
}
