using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SensitiveFlow.SourceGenerators;

/// <summary>
/// Incremental source generator that precomputes sensitive/retention member metadata at compile
/// time, avoiding per-call reflection scans at runtime.
/// </summary>
[Generator]
public sealed class SensitiveMemberGenerator : IIncrementalGenerator
{
    private const string PersonalDataAttributeName = "SensitiveFlow.Core.Attributes.PersonalDataAttribute";
    private const string SensitiveDataAttributeName = "SensitiveFlow.Core.Attributes.SensitiveDataAttribute";
    private const string RetentionDataAttributeName = "SensitiveFlow.Core.Attributes.RetentionDataAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Cache AttributeSymbols separately so they are recomputed only when the compilation
        // changes (e.g. a package add/remove), not on every keystroke.
        var attributeSymbols = context.CompilationProvider.Select(static (compilation, _) => new AttributeSymbols(
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

        // Combine the two independent pipelines — each is cached separately.
        var combined = candidateTypes.Combine(attributeSymbols);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (types, attrs) = source;
            Emit(spc, types, attrs);
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
        ImmutableArray<INamedTypeSymbol> types,
        AttributeSymbols attributeSymbols)
    {
        if (attributeSymbols.PersonalData is null || attributeSymbols.SensitiveData is null || attributeSymbols.RetentionData is null)
        {
            return;
        }

        var uniqueTypes = types
            .Where(t => t is not null)
            // Skip open generic definitions (class Foo<T>): typeof(Foo<T>) is not legal
            // in C# without a concrete T, and typeof(Foo<>) cannot be looked up by closed
            // instantiations at runtime. The reflection fallback in SensitiveMemberCache
            // handles closed generic types correctly without needing pre-registration.
            .Where(t => !t!.IsUnboundGenericType && t.TypeParameters.Length == 0)
            .Distinct((IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default)
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

        foreach (var (name, attributes) in EnumerateAnnotatedProperties(type))
        {
            if (HasAttribute(attributes, symbols.PersonalData) || HasAttribute(attributes, symbols.SensitiveData))
            {
                sensitiveProperties.Add(name);
            }

            var retention = attributes.FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.RetentionData));
            if (retention is not null && !retentionProperties.ContainsKey(name))
            {
                retentionProperties[name] = BuildRetentionEntry(name, retention);
            }
        }

        if (sensitiveProperties.Count == 0 && retentionProperties.Count == 0)
        {
            return null;
        }

        return new TypeEntry(type, sensitiveProperties.OrderBy(n => n, StringComparer.Ordinal).ToImmutableArray(),
            retentionProperties.Values.OrderBy(r => r.PropertyName, StringComparer.Ordinal).ToImmutableArray());
    }

    /// <summary>
    /// Yields each public instance property with the union of attributes declared on the property
    /// itself and on the corresponding property in any implemented interface. This makes attribute
    /// declarations on interfaces visible to implementing classes — without requiring the user
    /// to re-declare the attribute.
    /// </summary>
    private static IEnumerable<(string Name, ImmutableArray<AttributeData> Attributes)> EnumerateAnnotatedProperties(INamedTypeSymbol type)
    {
        var perProperty = new Dictionary<string, List<AttributeData>>(StringComparer.Ordinal);

        // Class hierarchy: collect per-property attributes from the most-derived declaration.
        var ownedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol property || !IsRelevantProperty(property))
                {
                    continue;
                }

                if (!ownedNames.Add(property.Name))
                {
                    continue;
                }

                if (!perProperty.TryGetValue(property.Name, out var list))
                {
                    list = new List<AttributeData>();
                    perProperty[property.Name] = list;
                }

                list.AddRange(property.GetAttributes());
            }
        }

        // Interfaces: merge attributes from the interface property into the implementing entry,
        // and surface interface-only properties (e.g. explicit implementations) as their own entries.
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is not IPropertySymbol property || !IsRelevantProperty(property))
                {
                    continue;
                }

                if (!perProperty.TryGetValue(property.Name, out var list))
                {
                    list = new List<AttributeData>();
                    perProperty[property.Name] = list;
                }

                list.AddRange(property.GetAttributes());
            }
        }

        foreach (var pair in perProperty)
        {
            yield return (pair.Key, pair.Value.ToImmutableArray());
        }
    }

    private static bool IsRelevantProperty(IPropertySymbol property)
        => !property.IsStatic
        && property.DeclaredAccessibility == Accessibility.Public
        && !property.IsIndexer;

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

    // IEquatable implemented so the incremental pipeline can detect equality changes
    // and skip re-emitting source when nothing has changed.
    private sealed class AttributeSymbols : IEquatable<AttributeSymbols>
    {
        public INamedTypeSymbol? PersonalData { get; }
        public INamedTypeSymbol? SensitiveData { get; }
        public INamedTypeSymbol? RetentionData { get; }

        public AttributeSymbols(INamedTypeSymbol? personalData, INamedTypeSymbol? sensitiveData, INamedTypeSymbol? retentionData)
        {
            PersonalData = personalData;
            SensitiveData = sensitiveData;
            RetentionData = retentionData;
        }

        public bool Equals(AttributeSymbols? other)
            => other is not null
            && SymbolEqualityComparer.Default.Equals(PersonalData, other.PersonalData)
            && SymbolEqualityComparer.Default.Equals(SensitiveData, other.SensitiveData)
            && SymbolEqualityComparer.Default.Equals(RetentionData, other.RetentionData);

        public override bool Equals(object? obj) => Equals(obj as AttributeSymbols);

        public override int GetHashCode()
        {
            var h1 = PersonalData is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(PersonalData);
            var h2 = SensitiveData is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(SensitiveData);
            var h3 = RetentionData is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(RetentionData);
            unchecked
            {
                return ((h1 * 397) ^ h2) * 397 ^ h3;
            }
        }
    }

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
