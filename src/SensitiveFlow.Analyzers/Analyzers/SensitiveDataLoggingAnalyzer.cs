using System.Collections.Immutable;
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
        if (context.Operation is not IInvocationOperation invocation)
        {
            return;
        }

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

            if (sensitiveMember is null || argument.Syntax.GetLocation() is not { } location)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.SensitiveDataLoggedDirectly,
                location,
                sensitiveMember.Name));
        }
    }

    private static bool IsLoggingCall(IMethodSymbol method)
    {
        var methodName = method.Name;
        if (!methodName.StartsWith("Log", StringComparison.Ordinal))
        {
            return false;
        }

        return method.ContainingType.Name is "LoggerExtensions" or "ILogger";
    }
}
