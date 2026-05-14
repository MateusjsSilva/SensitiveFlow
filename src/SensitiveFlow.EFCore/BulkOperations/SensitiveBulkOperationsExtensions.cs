using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.EFCore.BulkOperations;

/// <summary>
/// Audited counterparts to EF Core's <c>ExecuteUpdateAsync</c> and <c>ExecuteDeleteAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// EF Core's bulk operations translate directly to SQL <c>UPDATE</c>/<c>DELETE</c> and skip
/// the <c>ChangeTracker</c>, so the <see cref="Interceptors.SensitiveDataAuditInterceptor"/>
/// — which runs in <c>SaveChanges</c> — never observes them. Using them on entities that
/// hold personal or sensitive data therefore creates a silent audit gap.
/// </para>
/// <para>
/// These helpers close the gap. Before running the modification they project the affected
/// subjects with a single <c>SELECT</c>, then emit one <see cref="AuditRecord"/> per
/// (subject, annotated field). The fan-out matches what a <c>SaveChanges</c>-based update
/// of the same entities would produce.
/// </para>
/// <para>
/// <b>Subjects identifier:</b> the entity must expose a <c>DataSubjectId</c> (or <c>UserId</c>
/// as a legacy alias) property. Without it the audit row cannot be tied back to a person,
/// which defeats DSAR and right-to-erasure queries — the helper throws rather than emit
/// records that are not addressable per subject.
/// </para>
/// <para>
/// <b>Cost guard:</b> the prefetch <c>SELECT</c> and the audit-record fan-out are bounded by
/// <see cref="SensitiveBulkOperationsOptions.MaxAuditedRows"/>. Operations that would touch
/// more subjects throw so the caller can make an explicit decision.
/// </para>
/// </remarks>
public static class SensitiveBulkOperationsExtensions
{
    /// <summary>
    /// Tag attached to the underlying queries so <see cref="SensitiveBulkOperationsGuardInterceptor"/>
    /// can recognize that the call went through an auditing helper. The value is intentionally
    /// awkward to type by hand to discourage opting out by attaching the same tag manually.
    /// </summary>
    internal const string AuditedTag = "__SensitiveFlow:Audited__";

    /// <summary>
    /// Runs <see cref="EntityFrameworkQueryableExtensions.ExecuteUpdateAsync"/> while emitting
    /// an <see cref="AuditRecord"/> for every (subject, annotated field) pair that is
    /// modified by the call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <typeparamref name="TEntity"/> has no annotated members the call is forwarded
    /// directly to EF Core with no extra <c>SELECT</c> and no audit overhead.
    /// </para>
    /// <para>
    /// The audit record's <see cref="AuditRecord.Field"/> is reported only for properties that
    /// are both targeted by the <c>SetProperty</c> calls and annotated with
    /// <see cref="PersonalDataAttribute"/> or <see cref="SensitiveDataAttribute"/>.
    /// Non-sensitive setters do not produce audit records.
    /// </para>
    /// </remarks>
    public static async Task<int> ExecuteUpdateAuditedAsync<TEntity>(
        this IQueryable<TEntity> source,
        Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setters,
        IAuditStore auditStore,
        IAuditContext auditContext,
        SensitiveBulkOperationsOptions? options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(setters);
        ArgumentNullException.ThrowIfNull(auditStore);
        ArgumentNullException.ThrowIfNull(auditContext);

        options ??= new SensitiveBulkOperationsOptions();

        var sensitiveProperties = SensitiveMemberCache.GetSensitiveProperties(typeof(TEntity));
        if (sensitiveProperties.Count == 0)
        {
            return await source.TagWith(AuditedTag).ExecuteUpdateAsync(setters, cancellationToken);
        }

        var setProperties = ExtractSetProperties(setters);
        var affectedSensitive = sensitiveProperties
            .Where(p => setProperties.Contains(p.Name))
            .ToList();

        if (affectedSensitive.Count == 0)
        {
            // The entity is annotated but this particular call only touches non-sensitive
            // columns, so there is nothing meaningful to audit. We still tag the query so
            // the guard interceptor lets it through.
            return await source.TagWith(AuditedTag).ExecuteUpdateAsync(setters, cancellationToken);
        }

        var subjects = await CollectSubjectsAsync(source, options.MaxAuditedRows, cancellationToken);

        var affected = await source.TagWith(AuditedTag).ExecuteUpdateAsync(setters, cancellationToken);

        await EmitAuditAsync(
            subjects,
            typeof(TEntity).Name,
            affectedSensitive,
            AuditOperation.Update,
            auditStore,
            auditContext,
            cancellationToken);

        return affected;
    }

    /// <summary>
    /// Runs <see cref="EntityFrameworkQueryableExtensions.ExecuteDeleteAsync"/> while emitting
    /// a delete <see cref="AuditRecord"/> for every (subject, annotated field) pair removed
    /// by the call.
    /// </summary>
    public static async Task<int> ExecuteDeleteAuditedAsync<TEntity>(
        this IQueryable<TEntity> source,
        IAuditStore auditStore,
        IAuditContext auditContext,
        SensitiveBulkOperationsOptions? options = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(auditStore);
        ArgumentNullException.ThrowIfNull(auditContext);

        options ??= new SensitiveBulkOperationsOptions();

        var sensitiveProperties = SensitiveMemberCache.GetSensitiveProperties(typeof(TEntity));
        if (sensitiveProperties.Count == 0)
        {
            return await source.TagWith(AuditedTag).ExecuteDeleteAsync(cancellationToken);
        }

        var subjects = await CollectSubjectsAsync(source, options.MaxAuditedRows, cancellationToken);

        var affected = await source.TagWith(AuditedTag).ExecuteDeleteAsync(cancellationToken);

        await EmitAuditAsync(
            subjects,
            typeof(TEntity).Name,
            sensitiveProperties,
            AuditOperation.Delete,
            auditStore,
            auditContext,
            cancellationToken);

        return affected;
    }

    /// <summary>
    /// Walks the <c>SetProperty(x =&gt; x.Member, value)</c> chain and returns the names of
    /// the properties that are being assigned.
    /// </summary>
    /// <remarks>
    /// We only need the property names — values are intentionally not captured because the
    /// old value is unknown (no row was fetched) and the new value lives inside the audit
    /// store's <see cref="AuditRecord.Details"/> only when a <see cref="RedactionAttribute"/>
    /// says it should. Mirroring that behavior in bulk mode would require a second SELECT
    /// per row and was rejected as part of the design.
    /// </remarks>
    private static HashSet<string> ExtractSetProperties<TEntity>(
        Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setters)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var current = setters.Body;

        while (current is MethodCallExpression call && call.Method.Name == nameof(SetPropertyCalls<TEntity>.SetProperty))
        {
            if (call.Arguments.Count >= 1 &&
                call.Arguments[0] is LambdaExpression propertyLambda &&
                propertyLambda.Body is MemberExpression member &&
                member.Member is PropertyInfo property)
            {
                names.Add(property.Name);
            }

            current = call.Object ?? (call.Arguments.Count > 0 ? call.Arguments[^1] : null)!;
            if (current is null)
            {
                break;
            }
        }

        return names;
    }

    /// <summary>
    /// Projects the source query down to its <c>DataSubjectId</c> (or <c>UserId</c> alias)
    /// column and materializes up to <paramref name="maxRows"/> values. Reads one extra row
    /// to detect that the operation exceeded the limit, in which case it throws so the
    /// caller can decide between raising the limit and narrowing the query.
    /// </summary>
    private static async Task<List<string>> CollectSubjectsAsync<TEntity>(
        IQueryable<TEntity> source,
        int maxRows,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var subjectSelector = BuildSubjectSelector<TEntity>();

        // +1 lets us tell "exactly at the limit" apart from "over the limit" without a
        // separate count query.
        var rows = await source
            .Select(subjectSelector)
            .Take(maxRows + 1)
            .ToListAsync(cancellationToken);

        if (rows.Count > maxRows)
        {
            throw new InvalidOperationException(
                $"Audited bulk operation on '{typeof(TEntity).Name}' would touch more than {maxRows} subjects. " +
                "Narrow the predicate, process in batches, or raise SensitiveBulkOperationsOptions.MaxAuditedRows explicitly. " +
                "The limit exists because every additional subject means one extra row in the audit store and one extra row in the prefetch SELECT.");
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (string.IsNullOrEmpty(rows[i]))
            {
                throw new InvalidOperationException(
                    $"Entity '{typeof(TEntity).Name}' has a row whose DataSubjectId (or UserId) is null or empty. " +
                    "Audit records require a stable subject identifier — set it before persisting.");
            }
        }

        return rows;
    }

    /// <summary>
    /// Builds <c>e =&gt; e.DataSubjectId.ToString()</c> (or the <c>UserId</c> alias) at
    /// runtime so EF Core can translate the projection to plain SQL.
    /// </summary>
    private static Expression<Func<TEntity, string>> BuildSubjectSelector<TEntity>()
    {
        var type = typeof(TEntity);
        var prop = type.GetProperty("DataSubjectId") ?? type.GetProperty("UserId");
        if (prop is null)
        {
            throw new InvalidOperationException(
                $"Entity '{type.Name}' has no 'DataSubjectId' (or 'UserId') property. " +
                "Add a stable subject identifier so bulk audit records can correlate rows reliably.");
        }

        ValidateDataSubjectIdType(prop, type);

        var parameter = Expression.Parameter(type, "e");
        Expression access = Expression.Property(parameter, prop);
        if (prop.PropertyType != typeof(string))
        {
            access = Expression.Call(access, nameof(object.ToString), Type.EmptyTypes);
        }

        return Expression.Lambda<Func<TEntity, string>>(access, parameter);
    }

    private static void ValidateDataSubjectIdType(PropertyInfo prop, Type entityType)
    {
        var propType = prop.PropertyType;

        var isString = propType == typeof(string);
        var isGuid = propType == typeof(Guid);
        var isNullableGuid = propType == typeof(Guid?);

        if (!isString && !isGuid && !isNullableGuid)
        {
            throw new InvalidOperationException(
                $"Entity '{entityType.Name}' property '{prop.Name}' has type '{propType.Name}'. " +
                "DataSubjectId must be 'string' or 'Guid' to ensure stable, globally unique identifiers. " +
                "Auto-increment integers can lead to ID collisions after recycle and corrupt the audit trail. " +
                "For non-string types, wrap in a Guid or convert to a stable string format.");
        }
    }

    private static async Task EmitAuditAsync(
        IReadOnlyList<string> subjects,
        string entityName,
        IReadOnlyList<PropertyInfo> fields,
        AuditOperation operation,
        IAuditStore auditStore,
        IAuditContext auditContext,
        CancellationToken cancellationToken)
    {
        if (subjects.Count == 0 || fields.Count == 0)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var actorId = auditContext.ActorId;
        var ipToken = auditContext.IpAddressToken;
        var details = $"Bulk {operation.ToString().ToLowerInvariant()} via SensitiveBulkOperations helper.";

        var records = new List<AuditRecord>(subjects.Count * fields.Count);
        foreach (var subject in subjects)
        {
            foreach (var property in fields)
            {
                records.Add(new AuditRecord
                {
                    DataSubjectId = subject,
                    Entity = entityName,
                    Field = property.Name,
                    Operation = operation,
                    Timestamp = timestamp,
                    ActorId = actorId,
                    IpAddressToken = ipToken,
                    Details = details,
                });
            }
        }

        if (auditStore is IBatchAuditStore batch)
        {
            await batch.AppendRangeAsync(records, cancellationToken);
            return;
        }

        foreach (var record in records)
        {
            await auditStore.AppendAsync(record, cancellationToken);
        }
    }

}
