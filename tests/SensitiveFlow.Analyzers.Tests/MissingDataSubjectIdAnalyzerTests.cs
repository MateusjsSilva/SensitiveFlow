using FluentAssertions;
using SensitiveFlow.Analyzers.Analyzers;
using Xunit;

namespace SensitiveFlow.Analyzers.Tests;

public sealed class MissingDataSubjectIdAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnostic_WhenEntityHasSensitiveMembersButNoSubjectId()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    public int Id { get; set; }

    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingDataSubjectIdAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0003");
    }

    [Fact]
    public async Task DoesNotReport_WhenEntityHasDataSubjectId()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingDataSubjectIdAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0003");
    }

    [Fact]
    public async Task DoesNotReport_WhenEntityHasUserIdLegacyAlias()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class Customer
{
    public string UserId { get; set; } = string.Empty;

    [SensitiveData]
    public string TaxId { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class SensitiveDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingDataSubjectIdAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0003");
    }

    [Fact]
    public async Task DoesNotReport_WhenEntityHasNoSensitiveMembers()
    {
        const string source = """
public sealed class Plain
{
    public int Id { get; set; }

    [System.Obsolete]
    public string Name { get; set; } = string.Empty;
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingDataSubjectIdAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0003");
    }

    [Fact]
    public async Task DoesNotReport_WhenSubjectIdIsInheritedFromBase()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public abstract class Subject
{
    public string DataSubjectId { get; set; } = string.Empty;
}

public sealed class Customer : Subject
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingDataSubjectIdAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0003");
    }
}
