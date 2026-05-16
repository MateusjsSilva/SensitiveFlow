using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SensitiveFlow.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    public static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        Dictionary<string, string>? editorconfig = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Ensure core references exist, regardless of runtime load order.
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));

        // Add SensitiveFlow.Core so attributes like [PersonalData] / [SensitiveData] are resolvable.
        var coreAssembly = typeof(SensitiveFlow.Core.Attributes.PersonalDataAttribute).Assembly;
        references.Add(MetadataReference.CreateFromFile(coreAssembly.Location));

        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests",
            syntaxTrees: [syntaxTree],
            references: references.Distinct(MetadataReferencePathComparer.Instance),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        if (editorconfig != null)
        {
            var configProvider = new TestAnalyzerConfigOptionsProvider(editorconfig, syntaxTree);
            var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, configProvider);
            var analyzers = compilation.WithAnalyzers(
                [analyzer],
                analyzerOptions);

            return await analyzers.GetAnalyzerDiagnosticsAsync();
        }

        var diagnostics = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics;
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Dictionary<string, string> _options;
        private readonly SyntaxTree _syntaxTree;

        public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> options, SyntaxTree syntaxTree)
        {
            _options = options;
            _syntaxTree = syntaxTree;
        }

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            if (tree == _syntaxTree)
            {
                return new TestAnalyzerConfigOptions(_options);
            }

            return new TestAnalyzerConfigOptions(new Dictionary<string, string>());
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return new TestAnalyzerConfigOptions(new Dictionary<string, string>());
        }

        public override AnalyzerConfigOptions GlobalOptions =>
            new TestAnalyzerConfigOptions(new Dictionary<string, string>());
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _options;

        public TestAnalyzerConfigOptions(Dictionary<string, string> options)
        {
            _options = options;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return _options.TryGetValue(key, out value!);
        }
    }

    private sealed class MetadataReferencePathComparer : IEqualityComparer<MetadataReference>
    {
        public static readonly MetadataReferencePathComparer Instance = new();

        public bool Equals(MetadataReference? x, MetadataReference? y)
        {
            return GetPath(x).Equals(GetPath(y), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(MetadataReference obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(GetPath(obj));
        }

        private static string GetPath(MetadataReference? reference)
        {
            return (reference as PortableExecutableReference)?.FilePath ?? string.Empty;
        }
    }
}
