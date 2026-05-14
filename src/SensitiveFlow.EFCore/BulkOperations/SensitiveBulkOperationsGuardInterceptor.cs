using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.EFCore.BulkOperations;

/// <summary>
/// EF Core <see cref="IQueryExpressionInterceptor"/> that refuses raw
/// <c>ExecuteUpdateAsync</c>/<c>ExecuteDeleteAsync</c> against entities carrying
/// <see cref="Core.Attributes.PersonalDataAttribute"/> or
/// <see cref="Core.Attributes.SensitiveDataAttribute"/> annotations.
/// </summary>
/// <remarks>
/// <para>
/// EF Core's bulk operations bypass the <c>ChangeTracker</c> and the
/// <see cref="Interceptors.SensitiveDataAuditInterceptor"/>, so calling them directly on
/// annotated entities silently drops audit records that the rest of the library guarantees.
/// This interceptor detects that pattern at query-compilation time and fails fast with a
/// message that points the developer at <see cref="SensitiveBulkOperationsExtensions"/>.
/// </para>
/// <para>
/// The auditing helpers in <see cref="SensitiveBulkOperationsExtensions"/> tag their queries
/// with <see cref="SensitiveBulkOperationsExtensions.AuditedTag"/>. When the guard sees that
/// tag it lets the query through.
/// </para>
/// <para>
/// Disable per-process via <see cref="SensitiveBulkOperationsOptions.RequireExplicitAuditing"/>
/// when an application provides its own auditing path in front of EF Core.
/// </para>
/// </remarks>
public sealed class SensitiveBulkOperationsGuardInterceptor : IQueryExpressionInterceptor
{
    private readonly SensitiveBulkOperationsOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="SensitiveBulkOperationsGuardInterceptor"/>.
    /// </summary>
    public SensitiveBulkOperationsGuardInterceptor(SensitiveBulkOperationsOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Expression QueryCompilationStarting(
        Expression queryExpression,
        QueryExpressionEventData eventData)
    {
        if (!_options.RequireExplicitAuditing)
        {
            return queryExpression;
        }

        var visitor = new BulkOperationVisitor();
        visitor.Visit(queryExpression);

        if (visitor.UnauditedBulkTarget is { } entityType)
        {
            throw new InvalidOperationException(
                $"Direct {visitor.OperationName} on '{entityType.Name}' is blocked because the entity has personal or sensitive data annotations. " +
                $"Use {nameof(SensitiveBulkOperationsExtensions)}.{(visitor.OperationName == "ExecuteUpdate" ? nameof(SensitiveBulkOperationsExtensions.ExecuteUpdateAuditedAsync) : nameof(SensitiveBulkOperationsExtensions.ExecuteDeleteAuditedAsync))} " +
                $"so audit records are emitted, or set SensitiveBulkOperationsOptions.RequireExplicitAuditing = false if the application audits bulk operations elsewhere.");
        }

        return queryExpression;
    }

    private sealed class BulkOperationVisitor : ExpressionVisitor
    {
        private const string MethodExecuteUpdate = "ExecuteUpdate";
        private const string MethodExecuteUpdateAsync = "ExecuteUpdateAsync";
        private const string MethodExecuteDelete = "ExecuteDelete";
        private const string MethodExecuteDeleteAsync = "ExecuteDeleteAsync";

        public Type? UnauditedBulkTarget { get; private set; }
        public string OperationName { get; private set; } = string.Empty;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var name = node.Method.Name;
            if (name is MethodExecuteUpdate or MethodExecuteUpdateAsync
                or MethodExecuteDelete or MethodExecuteDeleteAsync)
            {
                if (!IsAuditedTagPresent(node) && node.Method.IsGenericMethod)
                {
                    var entityType = node.Method.GetGenericArguments()[0];
                    if (SensitiveMemberCache.GetSensitiveProperties(entityType).Count > 0)
                    {
                        UnauditedBulkTarget = entityType;
                        OperationName = name.EndsWith("Async", StringComparison.Ordinal)
                            ? name[..^"Async".Length]
                            : name;
                        // Stop descending — first hit is enough to fail the query.
                        return node;
                    }
                }
            }

            return base.VisitMethodCall(node);
        }

        private static bool IsAuditedTagPresent(Expression expression)
        {
            var current = expression;
            while (current is MethodCallExpression call)
            {
                if (call.Method.Name == "TagWith"
                    && call.Arguments.Count >= 2
                    && call.Arguments[1] is ConstantExpression constant
                    && constant.Value is string tag
                    && tag == SensitiveBulkOperationsExtensions.AuditedTag)
                {
                    return true;
                }

                current = call.Arguments.Count > 0 ? call.Arguments[0] : null!;
                if (current is null)
                {
                    break;
                }
            }

            return false;
        }
    }
}
