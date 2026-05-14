using FluentAssertions;
using Microsoft.CodeAnalysis;
using SensitiveFlow.Analyzers.Analyzers;
using Xunit;

namespace SensitiveFlow.Analyzers.Tests;

public sealed class MissingRedactionAttributeAnalyzerTests
{
    private static string FormatMessage(Diagnostic diagnostic)
    {
        return diagnostic.GetMessage();
    }

    #region Happy Path — Detects Missing [Redaction]

    [Fact]
    public async Task ReportsDiagnostic_WhenPersonalDataLacksRedaction()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class User
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0006");
        FormatMessage(diagnostics[0]).Should().Contain("Email");
        FormatMessage(diagnostics[0]).Should().Contain("PersonalData");
    }

    [Fact]
    public async Task ReportsDiagnostic_WhenSensitiveDataLacksRedaction()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class User
{
    [SensitiveData]
    public string TaxId { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class SensitiveDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0006");
        FormatMessage(diagnostics[0]).Should().Contain("TaxId");
        FormatMessage(diagnostics[0]).Should().Contain("SensitiveData");
    }

    #endregion

    #region Happy Path — Allows [Redaction]

    [Fact]
    public async Task DoesNotReport_WhenPersonalDataHasRedaction()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public sealed class User
{
    [PersonalData]
    [Redaction(ApiResponse = OutputRedactionAction.Mask)]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class RedactionAttribute : System.Attribute
    {
        public OutputRedactionAction ApiResponse { get; set; }
    }
}

namespace SensitiveFlow.Core.Enums
{
    public enum OutputRedactionAction { None, Mask, Redact, Omit, Pseudonymize }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0006");
    }

    [Fact]
    public async Task DoesNotReport_WhenSensitiveDataHasRedaction()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public sealed class User
{
    [SensitiveData]
    [Redaction(Logs = OutputRedactionAction.Redact)]
    public string TaxId { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class SensitiveDataAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class RedactionAttribute : System.Attribute
    {
        public OutputRedactionAction Logs { get; set; }
    }
}

namespace SensitiveFlow.Core.Enums
{
    public enum OutputRedactionAction { None, Mask, Redact, Omit, Pseudonymize }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0006");
    }

    #endregion

    #region Edge Cases — Multiple Properties

    [Fact]
    public async Task ReportsDiagnostic_ForEachPropertyMissingRedaction()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class User
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;

    [PersonalData]
    public string Phone { get; set; } = string.Empty;

    public string PublicField { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Where(d => d.Id == "SF0006").Should().HaveCount(2);
        diagnostics.Should().Satisfy(
            d => FormatMessage(d).Contains("Email"),
            d => FormatMessage(d).Contains("Phone"));
    }

    [Fact]
    public async Task ReportsDiagnostic_OnlyForMissingRedaction_WhenSomeMissing()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public sealed class User
{
    [PersonalData]
    [Redaction(ApiResponse = OutputRedactionAction.Mask)]
    public string Email { get; set; } = string.Empty;

    [PersonalData]
    public string Phone { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class RedactionAttribute : System.Attribute
    {
        public OutputRedactionAction ApiResponse { get; set; }
    }
}

namespace SensitiveFlow.Core.Enums
{
    public enum OutputRedactionAction { None, Mask, Redact, Omit, Pseudonymize }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Where(d => d.Id == "SF0006").Should().ContainSingle();
        FormatMessage(diagnostics.Single(d => d.Id == "SF0006")).Should().Contain("Phone");
    }

    #endregion

    #region Edge Cases — Ignores Non-Sensitive Properties

    [Fact]
    public async Task DoesNotReport_ForPropertiesWithoutSensitiveMarkers()
    {
        const string source = """
public sealed class User
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Address { get; set; } = string.Empty;
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0006");
    }

    [Fact]
    public async Task DoesNotReport_WhenOnlyOtherAttributesPresent()
    {
        const string source = """
using System.ComponentModel.DataAnnotations;

public sealed class User
{
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0006");
    }

    #endregion

    #region Complex Scenarios — Multiple Attributes

    [Fact]
    public async Task ReportsDiagnostic_EvenWithOtherAttributes()
    {
        const string source = """
using System.ComponentModel.DataAnnotations;
using SensitiveFlow.Core.Attributes;

public sealed class User
{
    [Required]
    [MaxLength(255)]
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().ContainSingle(d => d.Id == "SF0006");
    }

    [Fact]
    public async Task DoesNotReport_WhenRedactionPresentWithOtherAttributes()
    {
        const string source = """
using System.ComponentModel.DataAnnotations;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

public sealed class User
{
    [Required]
    [MaxLength(255)]
    [PersonalData]
    [Redaction(ApiResponse = OutputRedactionAction.Mask)]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class RedactionAttribute : System.Attribute
    {
        public OutputRedactionAction ApiResponse { get; set; }
    }
}

namespace SensitiveFlow.Core.Enums
{
    public enum OutputRedactionAction { None, Mask, Redact, Omit, Pseudonymize }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Should().NotContain(d => d.Id == "SF0006");
    }

    #endregion

    #region Complex Scenarios — Multiple Classes

    [Fact]
    public async Task ReportsDiagnostic_InMultipleClasses()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class User
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

public sealed class Order
{
    [PersonalData]
    public string CustomerEmail { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        diagnostics.Where(d => d.Id == "SF0006").Should().HaveCount(2);
    }

    #endregion

    #region Severity and Message

    [Fact]
    public async Task ReportedDiagnostic_HasCorrectSeverity()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class User
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        var sf0606 = diagnostics.Single(d => d.Id == "SF0006");
        sf0606.Severity.Should().Be(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ReportedDiagnostic_ContainsClearMessage()
    {
        const string source = """
using SensitiveFlow.Core.Attributes;

public sealed class User
{
    [PersonalData]
    public string Email { get; set; } = string.Empty;
}

namespace SensitiveFlow.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : System.Attribute { }
}
""";

        var diagnostics = await AnalyzerTestHarness.RunAsync(source, new MissingRedactionAttributeAnalyzer());

        var sf0006 = diagnostics.Single(d => d.Id == "SF0006");
        FormatMessage(sf0006).Should().Contain("Email");
        FormatMessage(sf0006).Should().Contain("[Redaction");
        FormatMessage(sf0006).Should().Contain("lacks");
    }

    #endregion
}
