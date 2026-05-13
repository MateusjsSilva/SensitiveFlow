using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using SensitiveFlow.Analyzers.Diagnostics;
using SensitiveFlow.Analyzers.Extensions;

namespace SensitiveFlow.Analyzers.Analyzers;

/// <summary>
/// Detects when personal or sensitive data is returned from endpoints that may not have
/// appropriate access controls, indicating a potential data leakage risk.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer warns when:
/// <list type="bullet">
///   <item><description>A method returns an entity or DTO with [PersonalData] or [SensitiveData]</description></item>
///   <item><description>The method lacks authorization attributes ([Authorize], [Authenticated], etc.)</description></item>
///   <item><description>The return type is directly exposed (not wrapped in a DTO or masking layer)</description></item>
/// </list>
/// </para>
/// <para>
/// This is a "cross-boundary" detector because it checks for sensitive data crossing the
/// application boundary (HTTP response) without proper safeguards.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrossBoundarySensitiveDataAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "SF0005";
    private const string Category = "SensitiveFlow.Security";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Sensitive data exposed from unprotected endpoint",
        messageFormat: "Method '{0}' returns sensitive field '{1}' without authorization protection.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Methods returning personal or sensitive data should have authorization attributes.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeMethodReturn, OperationKind.Return);
    }

    private static void AnalyzeMethodReturn(OperationAnalysisContext context)
    {
        var returnOp = (IReturnOperation)context.Operation;
        if (returnOp.ReturnedValue is null)
            return;

        // Check if containing method is an HTTP endpoint
        if (context.ContainingSymbol is not IMethodSymbol method || !IsHttpEndpointMethod(method))
            return;

        // Check if endpoint has authorization attributes
        if (HasAuthorizationAttribute(method))
            return;

        // Check if return value contains sensitive data
        if (!returnOp.ReturnedValue.TryFindSensitiveMember(out var sensitiveMember))
            return;

        // Warn about unprotected sensitive data return
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            returnOp.ReturnedValue.Syntax.GetLocation(),
            method.Name,
            sensitiveMember?.Name ?? "unknown"));
    }

    private static bool IsHttpEndpointMethod(IMethodSymbol method)
    {
        // Check for HTTP route attributes
        foreach (var attribute in method.GetAttributes())
        {
            var attrName = attribute.AttributeClass?.Name;
            if (attrName?.StartsWith("Http", StringComparison.Ordinal) == true ||
                attrName is "RouteAttribute" or "MapGetAttribute" or "MapPostAttribute" or
                    "MapPutAttribute" or "MapDeleteAttribute" or "EndpointAttribute" or
                    "MapAttribute")
            {
                return true;
            }
        }

        // Check containing type for [Controller] or [ApiController]
        var containingType = method.ContainingType;
        if (containingType is not null)
        {
            foreach (var attribute in containingType.GetAttributes())
            {
                var attrName = attribute.AttributeClass?.Name;
                if (attrName is "ControllerAttribute" or "ApiControllerAttribute")
                    return true;
            }
        }

        return false;
    }

    private static bool HasAuthorizationAttribute(IMethodSymbol method)
    {
        // Check method-level attributes first (highest priority)
        foreach (var attribute in method.GetAttributes())
        {
            if (IsAuthorizationAttribute(attribute.AttributeClass))
                return true;
        }

        // Check class-level attributes (inherited by all methods)
        var containingType = method.ContainingType;
        if (containingType is not null)
        {
            foreach (var attribute in containingType.GetAttributes())
            {
                if (IsAuthorizationAttribute(attribute.AttributeClass))
                    return true;
            }
        }

        // Check for [AllowAnonymous] which explicitly allows unauthenticated access
        if (HasAttributeByName(method, "AllowAnonymousAttribute") ||
            HasAttributeByName(containingType, "AllowAnonymousAttribute"))
        {
            return false; // Explicitly marked as anonymous
        }

        return false;
    }

    private static bool IsAuthorizationAttribute(INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
            return false;

        var name = attributeType.Name;
        return name is "AuthorizeAttribute" or "AuthenticatedAttribute" or "RequireAuthAttribute" or
                "RequireRoleAttribute" or "RequirePermissionAttribute" or "AuthorizedAttribute" or
                "AuthenticationAttribute";
    }

    private static bool HasAttributeByName(ISymbol? symbol, string attributeName)
    {
        if (symbol is null)
            return false;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == attributeName)
                return true;
        }

        return false;
    }
}
