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
}
