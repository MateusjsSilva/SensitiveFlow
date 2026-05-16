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

    public static readonly DiagnosticDescriptor MissingDataSubjectId = new(
        id: "SF0003",
        title: "Entity with sensitive data must expose a DataSubjectId",
        messageFormat: "Entity '{0}' has [PersonalData]/[SensitiveData] members but no 'DataSubjectId' (or legacy 'UserId') property — compilation will fail",
        category: "Privacy",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "SensitiveDataAuditInterceptor requires a stable subject identifier. Compile-time validation enforces this requirement. Add a public DataSubjectId (or UserId for legacy compatibility) property to the entity.");

    public static readonly DiagnosticDescriptor PossibleUnannotatedPii = new(
        id: "SF0004",
        title: "Property name suggests personal data but is not annotated",
        messageFormat: "Property '{0}' on type '{1}' may contain personal data but is not annotated with [PersonalData] or [SensitiveData]",
        category: "Privacy",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Properties whose names match common PII patterns (Email, Phone, Name, TaxId, Cpf, Ssn, Passport, Address, BirthDate, etc.) should be annotated so the library can audit, mask, and redact them.");

    public static readonly DiagnosticDescriptor SensitiveDataReturnedWithoutAuthorization = new(
        id: "SF0005",
        title: "Sensitive data returned from endpoint without authorization",
        messageFormat: "Method '{0}' returns sensitive member '{1}' but its endpoint is not protected by an authorization attribute",
        category: "Privacy",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Endpoints that surface [PersonalData] or [SensitiveData] members should be guarded by [Authorize] (or an equivalent attribute) so the lack of authentication is not the reason personal data leaks.");

    public static readonly DiagnosticDescriptor MissingRedactionAttribute = new(
        id: "SF0006",
        title: "Sensitive data property is missing [Redaction] attribute",
        messageFormat: "Property '{0}' is marked with [{1}] but lacks [Redaction(...)] — it will be exposed unredacted in API responses, logs, and audit trails",
        category: "Privacy",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties annotated with [PersonalData] or [SensitiveData] must explicitly declare how they should be redacted in each context (API response, logs, audit, export). Without [Redaction], the default is OutputRedactionAction.None, meaning the full PII value is exposed everywhere, defeating the purpose of marking it sensitive.");
}
