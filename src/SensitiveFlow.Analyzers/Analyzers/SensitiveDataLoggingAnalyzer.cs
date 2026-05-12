using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using SensitiveFlow.Analyzers.Diagnostics;
using SensitiveFlow.Analyzers.Extensions;

namespace SensitiveFlow.Analyzers.Analyzers;

/// <summary>
/// Detects direct logging of members annotated as personal or sensitive data.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SensitiveDataLoggingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.SensitiveDataLoggedDirectly];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!IsLoggingCall(invocation.TargetMethod))
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (!argument.Value.TryFindSensitiveMember(out var sensitiveMember))
            {
                continue;
            }

            if (sensitiveMember!.HasAllowSensitiveLoggingAttribute())
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.SensitiveDataLoggedDirectly,
                argument.Syntax.GetLocation(),
                sensitiveMember!.Name));
        }
    }

    private static bool IsLoggingCall(IMethodSymbol method)
    {
        var methodName = method.Name;
        if (!methodName.StartsWith("Log", StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = method.ContainingType;

        if (containingType.Name == "LoggerExtensions" &&
            containingType.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.Logging")
        {
            return true;
        }

        if (containingType.AllInterfaces.Any(i =>
                i.Name == "ILogger" &&
                i.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.Logging"))
        {
            return true;
        }

        return containingType.Name.StartsWith("ILogger", StringComparison.Ordinal) &&
               containingType.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.Logging";
    }
}
