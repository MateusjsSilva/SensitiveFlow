using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace SensitiveFlow.Analyzers.Extensions;

internal static class SymbolExtensions
{
    public static bool HasSensitiveDataAttribute(this ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (name is "SensitiveDataAttribute" or "PersonalDataAttribute")
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasSensitiveFlowIgnoreAttribute(this ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == "SensitiveFlowIgnoreAttribute")
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasAllowSensitiveLoggingAttribute(this ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == "AllowSensitiveLoggingAttribute")
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsSanitizationMethod(this IMethodSymbol method)
    {
        var name = method.Name;
        return name.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Redact", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Anonymize", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Pseudonymize", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Hash", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryFindSensitiveMember(this IOperation operation, out ISymbol? member)
    {
        member = null;

        if (operation is IInvocationOperation invocation &&
            invocation.TargetMethod.IsSanitizationMethod())
        {
            return false;
        }

        if (operation is IPropertyReferenceOperation propertyReference &&
            propertyReference.Property.HasSensitiveDataAttribute())
        {
            if (propertyReference.Property.HasSensitiveFlowIgnoreAttribute())
            {
                member = null;
                return false;
            }

            member = propertyReference.Property;
            return true;
        }

        if (operation is IFieldReferenceOperation fieldReference &&
            fieldReference.Field.HasSensitiveDataAttribute())
        {
            if (fieldReference.Field.HasSensitiveFlowIgnoreAttribute())
            {
                member = null;
                return false;
            }

            member = fieldReference.Field;
            return true;
        }

        foreach (var child in operation.ChildOperations)
        {
            if (child.TryFindSensitiveMember(out member))
            {
                return true;
            }
        }

        return false;
    }
}
