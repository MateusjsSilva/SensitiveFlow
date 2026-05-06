using Microsoft.CodeAnalysis;

namespace SensitiveFlow.Analyzers.Diagnostics;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor SensitiveDataLoggedDirectly = new(
        id: "SF0001",
        title: "Sensitive data should not be logged directly",
        messageFormat: "Sensitive member '{0}' is being logged without masking or redaction",
        category: "Privacy",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Avoid logging values annotated with [PersonalData] or [SensitiveData] without a masking/redaction transform.");

    public static readonly DiagnosticDescriptor SensitiveDataReturnedDirectly = new(
        id: "SF0002",
        title: "Sensitive data should not be returned directly in HTTP responses",
        messageFormat: "Sensitive member '{0}' is being returned in an HTTP response without masking or redaction",
        category: "Privacy",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Avoid returning values annotated with [PersonalData] or [SensitiveData] directly from API endpoints.");
}
