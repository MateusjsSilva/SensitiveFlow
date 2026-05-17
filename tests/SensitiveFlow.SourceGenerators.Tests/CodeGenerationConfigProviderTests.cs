using FluentAssertions;
using SensitiveFlow.SourceGenerators.Configuration;
using Xunit;

namespace SensitiveFlow.SourceGenerators.Tests;

public class CodeGenerationConfigProviderTests
{
    [Fact]
    public void Constructor_InitializesDefaultSnippets()
    {
        var provider = new CodeGenerationConfigProvider();

        provider.ConfigurationSnippets.Should().ContainKey("ProjectSetup");
    }

    [Fact]
    public void GetSnippet_ReturnsSnippet()
    {
        var provider = new CodeGenerationConfigProvider();

        var snippet = provider.GetSnippet("ProjectSetup");

        snippet.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetSnippet_ReturnsNullForUnknown()
    {
        var provider = new CodeGenerationConfigProvider();

        var snippet = provider.GetSnippet("UnknownSnippet");

        snippet.Should().BeNull();
    }

    [Fact]
    public void AddSnippet_RegistersCustomSnippet()
    {
        var provider = new CodeGenerationConfigProvider();
        var customCode = "public class Custom { }";

        provider.AddSnippet("CustomClass", customCode);

        provider.GetSnippet("CustomClass").Should().Be(customCode);
    }

    [Fact]
    public void AddSnippet_OverwritesExisting()
    {
        var provider = new CodeGenerationConfigProvider();
        provider.AddSnippet("Custom", "Old Code");

        provider.AddSnippet("Custom", "New Code");

        provider.GetSnippet("Custom").Should().Be("New Code");
    }

    [Fact]
    public void AddSnippet_ThrowsOnNullName()
    {
        var provider = new CodeGenerationConfigProvider();

        var act = () => provider.AddSnippet(null!, "code");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSnippet_ThrowsOnNullCode()
    {
        var provider = new CodeGenerationConfigProvider();

        var act = () => provider.AddSnippet("name", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetSetupGuide_ReturnsFormattedGuide()
    {
        var provider = new CodeGenerationConfigProvider();

        var guide = provider.GetSetupGuide();

        guide.Should().Contain("# SensitiveFlow Source Generator Setup");
        guide.Should().Contain("1.");
        guide.Should().Contain("2.");
    }

    [Fact]
    public void GetSetupGuide_ContainsAllInstructions()
    {
        var provider = new CodeGenerationConfigProvider();

        var guide = provider.GetSetupGuide();

        guide.Should().Contain("Install SensitiveFlow.Analyzers");
        guide.Should().Contain("EmitCompilerGeneratedFiles");
        guide.Should().Contain("[PersonalData]");
    }

    [Fact]
    public void ConfigurationSnippets_IsReadOnly()
    {
        var provider = new CodeGenerationConfigProvider();

        var snippets = provider.ConfigurationSnippets;

        snippets.Should().NotBeNull();
        // Cannot modify through property
        typeof(IReadOnlyDictionary<string, string>).IsAssignableFrom(snippets.GetType()).Should().BeTrue();
    }

    [Fact]
    public void AddSnippet_MultipleSnippets()
    {
        var provider = new CodeGenerationConfigProvider();

        provider.AddSnippet("Snippet1", "Code1");
        provider.AddSnippet("Snippet2", "Code2");
        provider.AddSnippet("Snippet3", "Code3");

        provider.ConfigurationSnippets.Should().HaveCountGreaterThanOrEqualTo(3);
    }
}
