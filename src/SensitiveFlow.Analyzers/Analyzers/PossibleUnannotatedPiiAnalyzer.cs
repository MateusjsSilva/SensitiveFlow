using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SensitiveFlow.Analyzers.Diagnostics;

namespace SensitiveFlow.Analyzers.Analyzers;

/// <summary>
/// Detects public properties whose names suggest they contain personal data
/// but are not annotated with <c>[PersonalData]</c> or <c>[SensitiveData]</c>.
/// Severity is Info — this is a hint, not a requirement.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PossibleUnannotatedPiiAnalyzer : DiagnosticAnalyzer
{
    private static readonly HashSet<string> PiiNamePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "EmailAddress", "Mail",
        "Phone", "PhoneNumber", "Telephone", "Mobile", "CellPhone",
        "Name", "FirstName", "LastName", "FullName", "MiddleName",
        "TaxId", "Cpf", "Cnpj", "Ssn", "SocialSecurity", "Nin",
        "Passport", "PassportNumber",
        "Address", "Street", "City", "State", "PostalCode", "ZipCode", "Zip",
        "BirthDate", "DateOfBirth", "Birthday", "Dob",
        "IpAddress", "Ip",
        "Document", "DocumentNumber", "IdNumber",
        "Gender", "Sex",
        "Nationality", "Country",
        "Photo", "Picture", "Avatar", "Image",
        "Biometric", "Fingerprint",
        "DriverLicense", "LicenseNumber",
    };

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.PossibleUnannotatedPii];

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

        // Only inspect public instance properties on non-abstract classes.
        if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public)
        {
            return;
        }

        if (property.ContainingType is not { TypeKind: TypeKind.Class, IsAbstract: false })
        {
            return;
        }

        // Skip properties that are already annotated.
        if (property.GetAttributes().Any(static a =>
        {
            var name = a.AttributeClass?.Name;
            return name is "PersonalDataAttribute" or "SensitiveDataAttribute";
        }))
        {
            return;
        }

        // Check if the property name matches a known PII pattern.
        if (!PiiNamePatterns.Contains(property.Name))
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.PossibleUnannotatedPii,
            property.Locations.FirstOrDefault(),
            property.Name,
            property.ContainingType.Name);

        context.ReportDiagnostic(diagnostic);
    }
}
