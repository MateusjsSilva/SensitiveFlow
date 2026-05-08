using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SensitiveFlow.SourceGenerators;

[Generator]
public sealed class SensitiveMemberGenerator : IIncrementalGenerator
{
    private const string PersonalDataAttributeName = "SensitiveFlow.Core.Attributes.PersonalDataAttribute";
    private const string SensitiveDataAttributeName = "SensitiveFlow.Core.Attributes.SensitiveDataAttribute";
    private const string RetentionDataAttributeName = "SensitiveFlow.Core.Attributes.RetentionDataAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var attributes = context.CompilationProvider.Select(static (compilation, _) => new AttributeSymbols(
            compilation.GetTypeByMetadataName(PersonalDataAttributeName),
            compilation.GetTypeByMetadataName(SensitiveDataAttributeName),
            compilation.GetTypeByMetadataName(RetentionDataAttributeName)));

        var candidateTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is PropertyDeclarationSyntax prop
                    && prop.AttributeLists.Count > 0
                    && prop.AttributeLists
                        .SelectMany(static al => al.Attributes)
                        .Any(static a => a.Name.ToString() is "PersonalData" or "SensitiveData" or "RetentionData"),
                static (ctx, _) => GetAnnotatedType(ctx))
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!)
            .Collect();

        var compilationAndCandidates = context.CompilationProvider.Combine(candidateTypes).Combine(attributes);

        context.RegisterSourceOutput(compilationAndCandidates, static (spc, source) =>
        {
            var ((compilation, types), attributeSymbols) = source;
            Emit(spc, compilation, types, attributeSymbols);
        });
    }

    private static INamedTypeSymbol? GetAnnotatedType(GeneratorSyntaxContext context)
    {
        if (context.Node is not PropertyDeclarationSyntax propertySyntax)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(propertySyntax) is not IPropertySymbol propertySymbol)
        {
            return null;
        }

        if (propertySymbol.ContainingType is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        return typeSymbol;
    }

    private static void Emit(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> types,
        AttributeSymbols attributeSymbols)
    {
        if (attributeSymbols.PersonalData is null || attributeSymbols.SensitiveData is null || attributeSymbols.RetentionData is null)
        {
            return;
        }

        var uniqueTypes = types
            .Where(t => t is not null)
            .Distinct(SymbolEqualityComparer.Default)
            .ToImmutableArray();

        if (uniqueTypes.Length == 0)
        {
            return;
        }

        var entries = new List<TypeEntry>();

        foreach (var type in uniqueTypes)
        {
            var entry = BuildEntry(type, attributeSymbols);
            if (entry is null)
            {
                continue;
            }

            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            return;
        }

        var source = Render(entries);
        context.AddSource("SensitiveFlow.Generated.SensitiveMembers.g.cs", source);
    }

    private static TypeEntry? BuildEntry(INamedTypeSymbol type, AttributeSymbols symbols)
    {
        var sensitiveProperties = new HashSet<string>(StringComparer.Ordinal);
        var retentionProperties = new Dictionary<string, RetentionEntry>(StringComparer.Ordinal);

        foreach (var property in EnumeratePublicInstanceProperties(type))
        {
            var attributes = property.GetAttributes();

            if (HasAttribute(attributes, symbols.PersonalData) || HasAttribute(attributes, symbols.SensitiveData))
            {
                sensitiveProperties.Add(property.Name);
            }

            var retention = attributes.FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.RetentionData));
            if (retention is not null)
            {
                var retentionEntry = BuildRetentionEntry(property.Name, retention);
                retentionProperties[property.Name] = retentionEntry;
            }
        }

        if (sensitiveProperties.Count == 0 && retentionProperties.Count == 0)
        {
            return null;
        }

        return new TypeEntry(type, sensitiveProperties.OrderBy(n => n, StringComparer.Ordinal).ToImmutableArray(),
            retentionProperties.Values.OrderBy(r => r.PropertyName, StringComparer.Ordinal).ToImmutableArray());
    }

    private static IEnumerable<IPropertySymbol> EnumeratePublicInstanceProperties(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol property)
                {
                    continue;
                }

                if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (property.IsIndexer)
                {
                    continue;
                }

                if (seen.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    private static bool HasAttribute(ImmutableArray<AttributeData> attributes, INamedTypeSymbol? target)
    {
        if (target is null)
        {
            return false;
        }

        foreach (var attr in attributes)
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, target))
            {
                return true;
            }
        }

        return false;
    }

    private static RetentionEntry BuildRetentionEntry(string propertyName, AttributeData attribute)
    {
        var years = 0;
        var months = 0;
        var policy = "SensitiveFlow.Core.Enums.RetentionPolicy.AnonymizeOnExpiration";

        foreach (var arg in attribute.NamedArguments)
        {
            switch (arg.Key)
            {
                case "Years":
                    years = (int)arg.Value.Value!;
                    break;
                case "Months":
                    months = (int)arg.Value.Value!;
                    break;
                case "Policy":
                    policy = ResolveEnumExpression(arg.Value);
                    break;
            }
        }

        return new RetentionEntry(propertyName, years, months, policy);
    }

    private static string ResolveEnumExpression(TypedConstant constant)
    {
        if (constant.Value is null || constant.Type is not INamedTypeSymbol enumType)
        {
            return "SensitiveFlow.Core.Enums.RetentionPolicy.AnonymizeOnExpiration";
        }

        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue && Equals(member.ConstantValue, constant.Value))
            {
                return $"{enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{member.Name}";
            }
        }

        return "SensitiveFlow.Core.Enums.RetentionPolicy.AnonymizeOnExpiration";
    }

    private static string Render(IReadOnlyList<TypeEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace SensitiveFlow.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class SensitiveFlowGeneratedMetadata");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        global::SensitiveFlow.Core.Reflection.SensitiveMemberCache.RegisterGeneratedMetadata(");
        sb.AppendLine("            new global::SensitiveFlow.Core.Reflection.GeneratedSensitiveType[]");
        sb.AppendLine("            {");

        foreach (var entry in entries)
        {
            sb.AppendLine("                new global::SensitiveFlow.Core.Reflection.GeneratedSensitiveType(");
            sb.AppendLine($"                    typeof({entry.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}),");

            sb.AppendLine("                    new string[]");
            sb.AppendLine("                    {");
            foreach (var name in entry.SensitivePropertyNames)
            {
                sb.AppendLine($"                        \"{name}\",");
            }
            sb.AppendLine("                    },");

            sb.AppendLine("                    new global::SensitiveFlow.Core.Reflection.GeneratedRetentionProperty[]");
            sb.AppendLine("                    {");
            foreach (var retention in entry.RetentionProperties)
            {
                sb.AppendLine("                        new global::SensitiveFlow.Core.Reflection.GeneratedRetentionProperty(");
                sb.AppendLine($"                            \"{retention.PropertyName}\",");
                sb.AppendLine($"                            {retention.Years},");
                sb.AppendLine($"                            {retention.Months},");
                sb.AppendLine($"                            {retention.PolicyExpression}),");
            }
            sb.AppendLine("                    }),");
        }

        sb.AppendLine("            });");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private sealed record AttributeSymbols(
        INamedTypeSymbol? PersonalData,
        INamedTypeSymbol? SensitiveData,
        INamedTypeSymbol? RetentionData);

    private sealed record TypeEntry(
        INamedTypeSymbol Type,
        ImmutableArray<string> SensitivePropertyNames,
        ImmutableArray<RetentionEntry> RetentionProperties);

    private sealed record RetentionEntry(
        string PropertyName,
        int Years,
        int Months,
        string PolicyExpression);
}
