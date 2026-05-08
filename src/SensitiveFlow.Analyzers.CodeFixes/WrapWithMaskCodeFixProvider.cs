using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SensitiveFlow.Analyzers.CodeFixes;

/// <summary>
/// Code-fix provider for SF0001 / SF0002 that wraps a sensitive expression with a
/// <c>.MaskEmail()</c> / <c>.MaskPhone()</c> / <c>.MaskName()</c> call (heuristic by member name)
/// or a generic <c>.MaskName()</c> as a safe default.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(WrapWithMaskCodeFixProvider))]
[Shared]
public sealed class WrapWithMaskCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("SF0001", "SF0002");

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is not ExpressionSyntax expression)
            {
                continue;
            }

            var memberName = ExtractMemberName(expression);
            var maskMethod = ChooseMaskMethod(memberName);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Wrap with .{maskMethod}()",
                    createChangedDocument: ct => WrapAsync(context.Document, expression, maskMethod, ct),
                    equivalenceKey: $"SF.WrapWith.{maskMethod}"),
                diagnostic);
        }
    }

    private static string ExtractMemberName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax id             => id.Identifier.ValueText,
        _                                   => string.Empty,
    };

    private static string ChooseMaskMethod(string memberName)
    {
        if (memberName.IndexOf("Email", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "MaskEmail";
        }
        if (memberName.IndexOf("Phone", System.StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("Cel", System.StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("Mobile", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "MaskPhone";
        }
        return "MaskName";
    }

    private static async Task<Document> WrapAsync(
        Document document,
        ExpressionSyntax expression,
        string maskMethod,
        System.Threading.CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newExpression = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName(maskMethod)))
            .WithTriviaFrom(expression);

        var newRoot = root.ReplaceNode(expression, newExpression);
        return document.WithSyntaxRoot(newRoot);
    }
}
