using System.Collections.Generic;

namespace SensitiveFlow.SourceGenerators.Configuration;

/// <summary>
/// Provides code generation configuration and documentation snippets.
/// </summary>
public sealed class CodeGenerationConfigProvider
{
    private readonly Dictionary<string, string> _configurationSnippets = new(StringComparer.Ordinal);
    private readonly List<string> _setupInstructions = new();

    /// <summary>
    /// Gets registered configuration snippets.
    /// </summary>
    public IReadOnlyDictionary<string, string> ConfigurationSnippets => _configurationSnippets;

    /// <summary>
    /// Initializes with default snippets.
    /// </summary>
    public CodeGenerationConfigProvider()
    {
        _setupInstructions.Add("Install SensitiveFlow.Analyzers package");
        _setupInstructions.Add("Set <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles> in .csproj");
        _setupInstructions.Add("Annotate entities with [PersonalData] and [Redaction]");
        _setupInstructions.Add("Rebuild to trigger generator");
        _setupInstructions.Add("Check obj/Debug/generated/ for generated files");

        _configurationSnippets["ProjectSetup"] = "<!-- Add SensitiveFlow.Analyzers to PackageReference -->";
    }

    /// <summary>
    /// Gets a configuration snippet by name.
    /// </summary>
    public string? GetSnippet(string name)
    {
        _configurationSnippets.TryGetValue(name, out var snippet);
        return snippet;
    }

    /// <summary>
    /// Adds a custom configuration snippet.
    /// </summary>
    public void AddSnippet(string name, string code)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (code == null) throw new ArgumentNullException(nameof(code));

        _configurationSnippets[name] = code;
    }

    /// <summary>
    /// Gets all setup instructions as a formatted guide.
    /// </summary>
    public string GetSetupGuide()
    {
        var guide = "# SensitiveFlow Source Generator Setup\n\n";
        for (int i = 0; i < _setupInstructions.Count; i++)
        {
            guide += $"{i + 1}. {_setupInstructions[i]}\n";
        }
        return guide;
    }
}
