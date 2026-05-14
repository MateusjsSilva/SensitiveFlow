using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SensitiveFlow.Analyzers.Diagnostics;
using SensitiveFlow.Analyzers.Extensions;

namespace SensitiveFlow.Analyzers.Analyzers;

/// <summary>
/// Detects when a property is marked with [PersonalData] or [SensitiveData] but lacks
/// an explicit [Redaction(...)] attribute, which could lead to silent data leakage.
/// </summary>
/// <remarks>
/// <para>
/// Rule SF0006 warns when:
/// <list type="bullet">
///   <item><description>A property has [PersonalData] or [SensitiveData] attribute</description></item>
///   <item><description>The property does NOT have [Redaction(...)] attribute</description></item>
/// </list>
/// </para>
/// <para>
/// Rationale: Without an explicit [Redaction] attribute, the library defaults to
/// OutputRedactionAction.None for all contexts (API response, logs, audit, export),
/// which means the full PII value will be exposed everywhere. This is unsafe.
/// </para>
/// <para>
/// Example of a safe property:
/// <code>
/// [PersonalData]
/// [Redaction(
///     ApiResponse = OutputRedactionAction.Mask,
///     Logs = OutputRedactionAction.Redact,
///     Audit = OutputRedactionAction.Mask,
///     Export = OutputRedactionAction.None)]
/// public string Email { get; set; }
/// </code>
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingRedactionAttributeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = DiagnosticDescriptors.MissingRedactionAttribute;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;

        // Check if property has [PersonalData] or [SensitiveData]
        var hasSensitiveMarker = false;
        string? sensitiveMarkerName = null;

        foreach (var attr in property.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName is "PersonalDataAttribute" or "SensitiveDataAttribute")
            {
                hasSensitiveMarker = true;
                sensitiveMarkerName = attrName;
                break;
            }
        }

        if (!hasSensitiveMarker)
            return;

        // Check if property has [Redaction(...)]
        var hasRedactionAttribute = false;
        foreach (var attr in property.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "RedactionAttribute")
            {
                hasRedactionAttribute = true;
                break;
            }
        }

        if (hasRedactionAttribute)
            return;

        // Property has [PersonalData]/[SensitiveData] but NO [Redaction] → Report diagnostic
        var diagnostic = Diagnostic.Create(
            Rule,
            property.Locations[0],
            property.Name,
            sensitiveMarkerName ?? "PersonalData");

        context.ReportDiagnostic(diagnostic);
    }
}
