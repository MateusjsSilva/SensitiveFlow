using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SensitiveFlow.Analyzers.Diagnostics;
using SensitiveFlow.Analyzers.Extensions;

namespace SensitiveFlow.Analyzers.Analyzers;

/// <summary>
/// Detects classes that declare <c>[PersonalData]</c>/<c>[SensitiveData]</c> members but no
/// <c>DataSubjectId</c> (or legacy <c>UserId</c>) property — the entity contract enforced
/// at runtime by <c>SensitiveDataAuditInterceptor</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingDataSubjectIdAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.MissingDataSubjectId];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        if (namedType.TypeKind != TypeKind.Class || namedType.IsAbstract)
        {
            return;
        }

        var hasSensitiveMember = namedType.GetMembers()
            .Any(static member => member is IPropertySymbol or IFieldSymbol && member.HasSensitiveDataAttribute());

        if (!hasSensitiveMember)
        {
            return;
        }

        if (HasSubjectIdentifier(namedType))
        {
            return;
        }

        var location = namedType.Locations.FirstOrDefault(static l => l.IsInSource) ?? Location.None;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MissingDataSubjectId,
            location,
            namedType.Name));
    }

    private static bool HasSubjectIdentifier(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol property || property.IsStatic)
                {
                    continue;
                }

                if (property.Name is "DataSubjectId" or "UserId")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
