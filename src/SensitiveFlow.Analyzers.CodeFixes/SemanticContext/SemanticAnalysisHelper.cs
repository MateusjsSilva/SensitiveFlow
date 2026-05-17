using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SensitiveFlow.Analyzers.CodeFixes.SemanticContext;

/// <summary>
/// Analyzes semantic context to suggest better masking methods.
/// </summary>
public sealed class SemanticAnalysisHelper
{
    /// <summary>
    /// Analyzes the context of an expression to determine the best masking method.
    /// </summary>
    public static string? DetermineBestMaskMethod(
        ExpressionSyntax expression,
        SemanticModel? semanticModel,
        string? heuristicMemberName = null)
    {
        if (expression is null)
        {
            return heuristicMemberName is not null ? ChooseByMemberName(heuristicMemberName) : null;
        }

        // Try semantic analysis first if available
        if (semanticModel is not null)
        {
            var typeInfo = semanticModel.GetTypeInfo(expression);
            if (typeInfo.Type is not null)
            {
                var method = ChooseByType(typeInfo.Type, heuristicMemberName);
                if (method is not null)
                {
                    return method;
                }
            }
        }

        // Fall back to heuristic analysis
        return heuristicMemberName is not null ? ChooseByMemberName(heuristicMemberName) : "Redact";
    }

    /// <summary>
    /// Chooses masking method based on type information.
    /// </summary>
    private static string? ChooseByType(ITypeSymbol typeSymbol, string? memberName)
    {
        var typeName = typeSymbol.Name.ToLowerInvariant();

        // Type-based detection
        if (typeName.Contains("email") || typeName.Contains("mail"))
        {
            return "MaskEmail";
        }

        if (typeName.Contains("phone") || typeName.Contains("tel"))
        {
            return "MaskPhone";
        }

        if (typeName.Contains("ssn") || typeName.Contains("social"))
        {
            return "MaskSsn";
        }

        if (typeName.Contains("credit") || typeName.Contains("card"))
        {
            return "MaskCreditCard";
        }

        if (typeName.Contains("ip") || typeName.Contains("address") && memberName?.Contains("ip") == true)
        {
            return "MaskIpAddress";
        }

        // Fall back to member name heuristic
        return memberName is not null ? ChooseByMemberName(memberName) : null;
    }

    /// <summary>
    /// Chooses masking method based on member/property name heuristics.
    /// </summary>
    private static string ChooseByMemberName(string name)
    {
        var lower = name.ToLowerInvariant();

        // Email patterns
        if (lower.Contains("email") || lower.Contains("mail"))
        {
            return "MaskEmail";
        }

        // Phone patterns
        if (lower.Contains("phone") || lower.Contains("tel") || lower.Contains("mobile"))
        {
            return "MaskPhone";
        }

        // SSN patterns
        if (lower.Contains("ssn") || lower.Contains("socialsecurity"))
        {
            return "MaskSsn";
        }

        // Credit card patterns
        if (lower.Contains("credit") || lower.Contains("card") || lower.Contains("cardnumber"))
        {
            return "MaskCreditCard";
        }

        // IP address patterns
        if (lower.Contains("ip") || lower.Contains("ipaddress"))
        {
            return "MaskIpAddress";
        }

        // Name patterns
        if (lower.Contains("name") && !lower.Contains("username"))
        {
            return "MaskName";
        }

        // Default safe masking
        return "Redact";
    }

    /// <summary>
    /// Gets the context type for an expression in an invocation.
    /// </summary>
    public static ExpressionContextType GetContextType(InvocationExpressionSyntax invocation)
    {
        if (invocation is null)
        {
            return ExpressionContextType.Unknown;
        }

        // Check if this is a logger call
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess?.Name.Identifier.ValueText.StartsWith("Log") == true)
        {
            return ExpressionContextType.Logging;
        }

        // Check if this is a response return
        if (invocation.Parent is ArgumentSyntax arg && arg.Parent is ArgumentListSyntax argList)
        {
            if (argList.Parent is InvocationExpressionSyntax parentInvoke)
            {
                var parentMember = parentInvoke.Expression as MemberAccessExpressionSyntax;
                if (parentMember?.Name.Identifier.ValueText == "Ok" ||
                    parentMember?.Name.Identifier.ValueText == "Created" ||
                    parentMember?.Name.Identifier.ValueText == "BadRequest")
                {
                    return ExpressionContextType.Response;
                }
            }
        }

        return ExpressionContextType.Unknown;
    }
}

/// <summary>
/// Context type for an expression.
/// </summary>
public enum ExpressionContextType
{
    /// <summary>Unknown context.</summary>
    Unknown = 0,

    /// <summary>Expression is logged.</summary>
    Logging = 1,

    /// <summary>Expression is in API response.</summary>
    Response = 2,

    /// <summary>Expression is in database query.</summary>
    Database = 3
}
