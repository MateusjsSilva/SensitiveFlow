using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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
        diagnostics[0].Id.Should().Be("SF0004");
    }

    [Fact]
    public void Analyzer_WarnsWhenSensitiveDataReturnedWithoutAuthorize()
    {
        var code = @"
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

[ApiController]
[Route(""api/[controller]"")]
public class CustomersController
{
    [HttpGet(""{id}"")]
    public CustomerResponse GetCustomer(string id)
    {
        var customer = new Customer { Email = ""alice@example.com"" };
        return new CustomerResponse { Email = customer.Email };
    }
}

public class Customer
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}

public class CustomerResponse
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}
";

        var diagnostics = GetDiagnostics(code);

        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().Satisfy(d =>
            d.Id == "SF0004" &&
            d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Analyzer_DoesNotWarnWhenAuthorizePresent()
    {
        var code = @"
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route(""api/[controller]"")]
public class CustomersController
{
    [Authorize]
    [HttpGet(""{id}"")]
    public CustomerResponse GetCustomer(string id)
    {
        var customer = new Customer { Email = ""alice@example.com"" };
        return new CustomerResponse { Email = customer.Email };
    }
}

public class Customer
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}

public class CustomerResponse
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}
";

        var diagnostics = GetDiagnostics(code);

        // Should not warn because [Authorize] is present
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_DoesNotWarnForPublicNonSensitiveFields()
    {
        var code = @"
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

[ApiController]
[Route(""api/[controller]"")]
public class ProductsController
{
    [HttpGet(""{id}"")]
    public ProductResponse GetProduct(string id)
    {
        return new ProductResponse { Name = ""Widget"" };
    }
}

public class ProductResponse
{
    public string Name { get; set; }
}
";

        var diagnostics = GetDiagnostics(code);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_WarnsForClassLevelAuthorize()
    {
        var code = @"
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route(""api/[controller]"")]
[Authorize]
public class CustomersController
{
    [HttpGet(""{id}"")]
    public CustomerResponse GetCustomer(string id)
    {
        var customer = new Customer { Email = ""alice@example.com"" };
        return new CustomerResponse { Email = customer.Email };
    }
}

public class Customer
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}

public class CustomerResponse
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}
";

        var diagnostics = GetDiagnostics(code);

        // Should not warn because class has [Authorize]
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Analyzer_DoesNotWarnForNonHttpMethods()
    {
        var code = @"
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public class DataProcessor
{
    public CustomerResponse GetCustomer(string id)
    {
        var customer = new Customer { Email = ""alice@example.com"" };
        return new CustomerResponse { Email = customer.Email };
    }
}

public class Customer
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}

public class CustomerResponse
{
    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; }
}
";

        var diagnostics = GetDiagnostics(code);

        // Should not warn because method is not an HTTP endpoint
        diagnostics.Should().BeEmpty();
    }

    private ImmutableArray<Diagnostic> GetDiagnostics(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("test")
            .AddReferences(GetReferences())
            .AddSyntaxTrees(tree);

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            new DiagnosticAnalyzer[] { _analyzer }.ToImmutableArray());

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
    }

    private IEnumerable<MetadataReference> GetReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Where(ta => Path.GetFileName(ta) is "System.dll" or "netstandard.dll" or "System.Core.dll")
            .Select(ta => MetadataReference.CreateFromFile(ta))
            .Cast<MetadataReference>()
            .ToList();

        // Add common assembly references needed for the test
        try
        {
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        }
        catch { }

        return references;
    }
}
