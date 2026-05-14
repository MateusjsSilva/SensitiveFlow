using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.EFCore.Interceptors;

/// <summary>
/// EF Core interceptor that automatically emits <see cref="AuditRecord"/> entries for
/// any entity property decorated with <see cref="PersonalDataAttribute"/> or
/// <see cref="SensitiveDataAttribute"/> when changes are saved.
/// </summary>
/// <remarks>
/// <b>IMPORTANT:</b> Always use <c>DbContext.SaveChangesAsync</c> instead of <c>SaveChanges</c> when using
/// this interceptor in ASP.NET Core or any async context. The sync overrides block the thread and can cause
/// deadlocks under high concurrency due to the async I/O required for audit record flushing.
/// Only use sync methods in console apps, Windows services, or offline batch processing.
/// </remarks>
public sealed class SensitiveDataAuditInterceptor : SaveChangesInterceptor
{
    private readonly IAuditStore _auditStore;
    private readonly IAuditContext _auditContext;
    private readonly IPseudonymizer? _pseudonymizer;
    private readonly ConditionalWeakTable<DbContext, PendingAuditRecords> _pendingRecords = new();

    /// <summary>
    /// Initializes a new instance of <see cref="SensitiveDataAuditInterceptor"/>.
    /// </summary>
    public SensitiveDataAuditInterceptor(
        IAuditStore auditStore,
        IAuditContext auditContext,
        IPseudonymizer? pseudonymizer = null)
    {
        _auditStore = auditStore;
        _auditContext = auditContext;
        _pseudonymizer = pseudonymizer;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            CaptureAuditRecords(eventData.Context);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>⚠ DEADLOCK RISK:</b> This override blocks the calling thread via <c>GetAwaiter().GetResult()</c>.
    /// In ASP.NET Core the thread-pool synchronization context makes blocking on async code unsafe under high concurrency.
    /// <b>DO NOT USE IN ASP.NET CORE.</b> Always use <c>DbContext.SaveChangesAsync</c> instead of <c>SaveChanges</c>.
    /// This method is safe only in console apps or background jobs.
    /// Consider using <see cref="SavingChangesAsync"/> instead for all production scenarios.
    /// </remarks>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            CaptureAuditRecords(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await FlushAuditRecordsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>⚠ DEADLOCK RISK:</b> Flushing audit records requires async I/O. To avoid the
    /// classic sync-over-async deadlock on ASP.NET Core's thread-pool, we run the flush
    /// via <see cref="Task.Run(Func{Task})"/>, which detaches from any captured
    /// <see cref="SynchronizationContext"/>. This is still a synchronous wait — prefer
    /// <c>SaveChangesAsync</c> in any concurrent host.
    /// </remarks>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
        {
            var context = eventData.Context;
            Task.Run(() => FlushAuditRecordsAsync(context, CancellationToken.None)).GetAwaiter().GetResult();
        }

        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
        {
            _pendingRecords.Remove(eventData.Context);
        }

        base.SaveChangesFailed(eventData);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _pendingRecords.Remove(eventData.Context);
        }

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void CaptureAuditRecords(DbContext context)
    {
        context.ChangeTracker.DetectChanges();

        // Filter by entity state AND by whether the type has any sensitive properties
        // before materializing, so non-sensitive entities never allocate a list entry.
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                     && SensitiveMemberCache.GetSensitiveProperties(e.Entity.GetType()).Count > 0)
            .ToList();

        var timestamp = DateTimeOffset.UtcNow;
        var actorId = _auditContext.ActorId;
        var ipToken = _auditContext.IpAddressToken;

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType();
            var sensitiveProperties = SensitiveMemberCache.GetSensitiveProperties(entityType);
            var entityName = entityType.Name;
            var operation = MapOperation(entry.State);
            var dataSubjectId = ResolveDataSubjectId(entry.Entity);

            foreach (var property in sensitiveProperties)
            {
                var auditAction = ResolveAuditAction(property);

                if (entry.State == EntityState.Modified)
                {
                    var propEntry = entry.Property(property.Name);
                    if (!propEntry.IsModified)
                    {
                        continue;
                    }
                }

                var record = new AuditRecord
                {
                    DataSubjectId = dataSubjectId,
                    Entity = entityName,
                    Field = property.Name,
                    Operation = operation,
                    Timestamp = timestamp,
                    ActorId = actorId,
                    IpAddressToken = ipToken,
                    Details = BuildAuditDetails(entry.Entity, property, auditAction)
                };

                var pending = _pendingRecords.GetOrCreateValue(context);
                pending.Records.Add(record);
            }
        }
    }

    private string? BuildAuditDetails(object entity, System.Reflection.PropertyInfo property, OutputRedactionAction action)
    {
        if (action == OutputRedactionAction.None)
        {
            return null;
        }

        var value = property.CanRead ? property.GetValue(entity) : null;
        var protectedValue = action switch
        {
            OutputRedactionAction.Redact => SensitiveFlowDefaults.RedactedPlaceholder,
            OutputRedactionAction.Mask => MaskValue(value, property.Name),
            OutputRedactionAction.Pseudonymize => PseudonymizeValue(value),
            _ => null,
        };

        return protectedValue is null
            ? $"Audit redaction action: {action}."
            : $"Audit redaction action: {action}; value: {protectedValue}.";
    }

    private string PseudonymizeValue(object? value)
    {
        var text = value?.ToString();
        if (string.IsNullOrEmpty(text) || _pseudonymizer is null)
        {
            return SensitiveFlowDefaults.RedactedPlaceholder;
        }

        return _pseudonymizer.Pseudonymize(text);
    }

    private static OutputRedactionAction ResolveAuditAction(System.Reflection.PropertyInfo property)
    {
        var contextual = property.GetCustomAttributes(typeof(RedactionAttribute), inherit: true)
            .OfType<RedactionAttribute>()
            .FirstOrDefault();
        return contextual?.ForContext(RedactionContext.Audit) ?? OutputRedactionAction.None;
    }

    private static string MaskValue(object? value, string propertyName)
    {
        var text = value?.ToString();
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            var at = text.IndexOf('@', StringComparison.Ordinal);
            return at > 1
                ? text[0] + new string('*', at - 1) + text[at..]
                : GenericMask(text);
        }

        return GenericMask(text);
    }

    private static string GenericMask(string text)
    {
        if (text.Length == 1)
        {
            return "*";
        }

        return string.Create(text.Length, text, static (span, source) =>
        {
            span[0] = source[0];
            for (var i = 1; i < span.Length; i++)
            {
                span[i] = '*';
            }
        });
    }

    private async Task FlushAuditRecordsAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (!_pendingRecords.TryGetValue(context, out var pending) || pending.Records.Count == 0)
        {
            return;
        }

        _pendingRecords.Remove(context);

        if (_auditStore is IBatchAuditStore batchStore)
        {
            await batchStore.AppendRangeAsync(pending.Records, cancellationToken);
            return;
        }

        foreach (var record in pending.Records)
        {
            await _auditStore.AppendAsync(record, cancellationToken);
        }
    }

    [ExcludeFromCodeCoverage]
    private static AuditOperation MapOperation(EntityState state) => state switch
    {
        EntityState.Added    => AuditOperation.Create,
        EntityState.Modified => AuditOperation.Update,
        EntityState.Deleted  => AuditOperation.Delete,
        _                    => AuditOperation.Access,
    };

    private static string ResolveDataSubjectId(object entity)
    {
        // The previous implementation also fell back to a property called "Id". That
        // turned out to be unsafe: EF-managed auto-increment keys can be assigned by the
        // provider (e.g. InMemory) before the interceptor runs, so the audit row would be
        // tagged with whatever the database happened to allocate — a value that has no
        // meaning to the data subject the row is supposed to be about. We now require
        // a property explicitly named DataSubjectId (or UserId as a legacy alias).
        var type = entity.GetType();
        var prop = type.GetProperty("DataSubjectId") ?? type.GetProperty("UserId");

        if (prop is null)
        {
            throw new InvalidOperationException(
                $"Entity '{type.Name}' has no 'DataSubjectId' (or 'UserId') property. " +
                "Add a stable subject identifier so the audit trail can correlate rows reliably; " +
                "the database-generated 'Id' is not used because it is not under the application's control at SaveChanges time.");
        }

        ValidateDataSubjectIdType(prop, type);

        var value = prop.GetValue(entity)?.ToString();
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Entity '{type.Name}' declares '{prop.Name}' but its value is null or empty at SaveChanges time. " +
                "Audit records require a stable subject identifier — set it before persisting.");
        }

        return value;
    }

    private static void ValidateDataSubjectIdType(System.Reflection.PropertyInfo prop, Type entityType)
    {
        var propType = prop.PropertyType;

        // Allow string and Guid (or Guid?)
        var isString = propType == typeof(string);
        var isGuid = propType == typeof(Guid);
        var isNullableGuid = propType == typeof(Guid?);

        if (!isString && !isGuid && !isNullableGuid)
        {
            throw new InvalidOperationException(
                $"Entity '{entityType.Name}' property '{prop.Name}' has type '{propType.Name}'. " +
                "DataSubjectId must be 'string' or 'Guid' to ensure stable, globally unique identifiers. " +
                "Auto-increment integers and other types can lead to ID collisions after recycle. " +
                "For non-string types, wrap in a Guid or convert to a stable string format (e.g., public string DataSubjectId => UserId.ToString(\"X\")).");
        }
    }

    private sealed class PendingAuditRecords
    {
        public List<AuditRecord> Records { get; } = [];
    }
}
