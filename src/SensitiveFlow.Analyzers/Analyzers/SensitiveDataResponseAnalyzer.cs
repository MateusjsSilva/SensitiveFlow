using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using SensitiveFlow.Analyzers.Diagnostics;
using SensitiveFlow.Analyzers.Extensions;

namespace SensitiveFlow.Analyzers.Analyzers;

/// <summary>
/// Detects direct HTTP response usage of members annotated as personal or sensitive data.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SensitiveDataResponseAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.SensitiveDataReturnedDirectly];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeReturn, OperationKind.Return);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocation)
        {
            return;
        }

        if (!IsHttpResponseFactoryCall(invocation.TargetMethod))
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (!argument.Value.TryFindSensitiveMember(out var sensitiveMember) || sensitiveMember is null)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.SensitiveDataReturnedDirectly,
                argument.Syntax.GetLocation(),
                sensitiveMember.Name));
        }
    }

    private static void AnalyzeReturn(OperationAnalysisContext context)
    {
        if (context.Operation is not IReturnOperation returnOperation || returnOperation.ReturnedValue is null)
        {
            return;
        }

        if (returnOperation.ReturnedValue is IInvocationOperation invocation &&
            IsHttpResponseFactoryCall(invocation.TargetMethod))
        {
            return;
        }

        if (context.ContainingSymbol is not IMethodSymbol method || !IsHttpEndpointMethod(method))
        {
            return;
        }

        if (!returnOperation.ReturnedValue.TryFindSensitiveMember(out var sensitiveMember) || sensitiveMember is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.SensitiveDataReturnedDirectly,
            returnOperation.ReturnedValue.Syntax.GetLocation(),
            sensitiveMember.Name));
    }

    private static bool IsHttpResponseFactoryCall(IMethodSymbol method)
    {
        var methodName = method.Name;
        if (methodName is not ("Ok" or "Json" or "Created" or "CreatedAtRoute" or "CreatedAtAction"))
        {
            return false;
        }

        return method.ContainingType.Name is "ControllerBase" or "Results" or "TypedResults";
    }

    private static bool IsHttpEndpointMethod(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (name is null)
            {
                continue;
            }

            if (name.StartsWith("Http", StringComparison.Ordinal) ||
                name is "RouteAttribute" or "EndpointAttribute")
            {
                return true;
            }
        }

        return false;
    }
}
