using FluentAssertions;
using SensitiveFlow.Analyzers.Analyzers;

namespace SensitiveFlow.Analyzers.Tests;

public sealed class PossibleUnannotatedPiiAnalyzerTests
{
    private readonly PossibleUnannotatedPiiAnalyzer _analyzer = new();

    [Fact]
    public async Task PropertyNamedEmail_WithoutAnnotation_ReportsSF0004()
    {
        const string source = """
            public class Customer
            {
                public string Email { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().ContainSingle(d => d.Id == "SF0004");
        diagnostics[0].GetMessage().Should().Contain("Email").And.Contain("Customer");
    }

    [Fact]
    public async Task PropertyNamedPhone_WithoutAnnotation_ReportsSF0004()
    {
        const string source = """
            public class Customer
            {
                public string Phone { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().ContainSingle(d => d.Id == "SF0004");
    }

    [Fact]
    public async Task PropertyNamedTaxId_WithoutAnnotation_ReportsSF0004()
    {
        const string source = """
            public class Customer
            {
                public string TaxId { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().ContainSingle(d => d.Id == "SF0004");
    }

    [Fact]
    public async Task PropertyNamedCpf_WithoutAnnotation_ReportsSF0004()
    {
        const string source = """
            public class Customer
            {
                public string Cpf { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().ContainSingle(d => d.Id == "SF0004");
    }

    [Fact]
    public async Task PropertyNamedBirthDate_WithoutAnnotation_ReportsSF0004()
    {
        const string source = """
            public class Customer
            {
                public DateTime BirthDate { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().ContainSingle(d => d.Id == "SF0004");
    }

    [Fact]
    public async Task PropertyNamedAddress_WithoutAnnotation_ReportsSF0004()
    {
        const string source = """
            public class Customer
            {
                public string Address { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().ContainSingle(d => d.Id == "SF0004");
    }

    [Fact]
    public async Task PropertyWithPersonalDataAttribute_DoesNotReport()
    {
        const string source = """
            using SensitiveFlow.Core.Attributes;

            public class Customer
            {
                [PersonalData]
                public string Email { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task PropertyWithSensitiveDataAttribute_DoesNotReport()
    {
        const string source = """
            using SensitiveFlow.Core.Attributes;

            public class Customer
            {
                [SensitiveData]
                public string TaxId { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task PropertyNotMatchingPiiPattern_DoesNotReport()
    {
        const string source = """
            public class Customer
            {
                public string Description { get; set; }
                public int Quantity { get; set; }
                public bool IsActive { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task StaticProperty_DoesNotReport()
    {
        const string source = """
            public class Customer
            {
                public static string Email { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task PrivateProperty_DoesNotReport()
    {
        const string source = """
            public class Customer
            {
                private string Email { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleUnannotatedProperties_ReportsAll()
    {
        const string source = """
            public class Customer
            {
                public string Email { get; set; }
                public string Phone { get; set; }
                public string Name { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().AllSatisfy(d => d.Id.Should().Be("SF0004"));
    }

    [Fact]
    public async Task AbstractClass_DoesNotReport()
    {
        const string source = """
            public abstract class BaseEntity
            {
                public string Email { get; set; }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, _analyzer);

        diagnostics.Should().BeEmpty();
    }
}
