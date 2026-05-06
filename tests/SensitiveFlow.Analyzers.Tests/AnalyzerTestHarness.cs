using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SensitiveFlow.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    public static async Task<ImmutableArray<Diagnostic>> RunAsync(string source, DiagnosticAnalyzer analyzer)
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

        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests",
            syntaxTrees: [syntaxTree],
            references: references.Distinct(MetadataReferencePathComparer.Instance),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics;
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
