using FluentAssertions;
using SensitiveFlow.Analyzers.CodeFixes.Configuration;
using Xunit;

namespace SensitiveFlow.Analyzers.CodeFixes.Tests;

public class CodeFixConfigurationTests
{
    [Fact]
    public void RecognizedMaskingMethods_HasDefaultMethods()
    {
        var config = new CodeFixConfiguration();

        config.RecognizedMaskingMethods.Should().Contain("MaskEmail");
        config.RecognizedMaskingMethods.Should().Contain("MaskPhone");
        config.RecognizedMaskingMethods.Should().Contain("Redact");
    }

    [Fact]
    public void AddCustomPattern_RegistersPattern()
    {
        var config = new CodeFixConfiguration();
        config.AddCustomPattern("email", "MaskEmail");

        config.CustomMaskingPatterns["email"].Should().Be("MaskEmail");
    }

    [Fact]
    public void GetMaskingMethodForProperty_ExactMatch()
    {
        var config = new CodeFixConfiguration();
        config.AddCustomPattern("customerEmail", "MaskEmail");

        var method = config.GetMaskingMethodForProperty("customerEmail");

        method.Should().Be("MaskEmail");
    }

    [Fact]
    public void GetMaskingMethodForProperty_WildcardMatch()
    {
        var config = new CodeFixConfiguration();
        config.AddCustomPattern("email", "MaskEmail");

        var method = config.GetMaskingMethodForProperty("userEmail");

        method.Should().Be("MaskEmail");
    }

    [Fact]
    public void GetMaskingMethodForProperty_NoMatch()
    {
        var config = new CodeFixConfiguration();

        var method = config.GetMaskingMethodForProperty("unknownField");

        method.Should().BeNull();
    }

    [Fact]
    public void IsRecognizedMaskingMethod_ReturnsTrueForKnownMethod()
    {
        var config = new CodeFixConfiguration();

        config.IsRecognizedMaskingMethod("MaskEmail").Should().BeTrue();
        config.IsRecognizedMaskingMethod("MaskPhone").Should().BeTrue();
    }

    [Fact]
    public void IsRecognizedMaskingMethod_ReturnsFalseForUnknown()
    {
        var config = new CodeFixConfiguration();

        config.IsRecognizedMaskingMethod("UnknownMethod").Should().BeFalse();
    }

    [Fact]
    public void ClearCustomPatterns_RemovesAllPatterns()
    {
        var config = new CodeFixConfiguration();
        config.AddCustomPattern("email", "MaskEmail");
        config.AddCustomPattern("phone", "MaskPhone");

        config.ClearCustomPatterns();

        config.CustomMaskingPatterns.Should().BeEmpty();
    }

    [Fact]
    public void ResetToDefaults_ClearsAndResetsFlags()
    {
        var config = new CodeFixConfiguration();
        config.AddCustomPattern("email", "MaskEmail");
        config.EnableSemanticAnalysis = false;
        config.EnableBatchFixes = false;

        config.ResetToDefaults();

        config.CustomMaskingPatterns.Should().BeEmpty();
        config.EnableSemanticAnalysis.Should().BeTrue();
        config.EnableBatchFixes.Should().BeTrue();
    }

    [Fact]
    public void CreateDefault_ReturnsNewInstance()
    {
        var config = CodeFixConfiguration.CreateDefault();

        config.Should().NotBeNull();
        config.CustomMaskingPatterns.Should().BeEmpty();
    }

    [Fact]
    public void CreateWithPatterns_InitializesPatterns()
    {
        var patterns = new Dictionary<string, string>
        {
            { "email", "MaskEmail" },
            { "phone", "MaskPhone" }
        };

        var config = CodeFixConfiguration.CreateWithPatterns(patterns);

        config.CustomMaskingPatterns.Should().HaveCount(2);
        config.GetMaskingMethodForProperty("email").Should().Be("MaskEmail");
    }

    [Fact]
    public void AddCustomPattern_ThrowsOnNullPattern()
    {
        var config = new CodeFixConfiguration();

        var act = () => config.AddCustomPattern(null!, "MaskEmail");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCustomPattern_ThrowsOnNullMethod()
    {
        var config = new CodeFixConfiguration();

        var act = () => config.AddCustomPattern("email", null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
