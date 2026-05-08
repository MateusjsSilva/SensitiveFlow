using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.EFCore.Interceptors;

/// <summary>
/// EF Core interceptor that automatically emits <see cref="AuditRecord"/> entries for
/// any entity property decorated with <see cref="PersonalDataAttribute"/> or
/// <see cref="SensitiveDataAttribute"/> when changes are saved.
/// </summary>
public sealed class SensitiveDataAuditInterceptor : SaveChangesInterceptor
{
    private readonly IAuditStore _auditStore;
    private readonly IAuditContext _auditContext;
    private readonly ConditionalWeakTable<DbContext, PendingAuditRecords> _pendingRecords = new();

    /// <summary>
    /// Initializes a new instance of <see cref="SensitiveDataAuditInterceptor"/>.
    /// </summary>
    public SensitiveDataAuditInterceptor(IAuditStore auditStore, IAuditContext auditContext)
    {
        _auditStore = auditStore;
        _auditContext = auditContext;
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
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
        {
            FlushAuditRecordsAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
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
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        var timestamp = DateTimeOffset.UtcNow;
        var actorId = _auditContext.ActorId;
        var ipToken = _auditContext.IpAddressToken;

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType();
            var sensitiveProperties = SensitiveMemberCache.GetSensitiveProperties(entityType);
            if (sensitiveProperties.Count == 0)
            {
                continue;
            }

            var entityName = entityType.Name;
            var operation = MapOperation(entry.State);
            var dataSubjectId = ResolveDataSubjectId(entry.Entity);

            foreach (var property in sensitiveProperties)
            {
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
                    IpAddressToken = ipToken
                };

                var pending = _pendingRecords.GetOrCreateValue(context);
                pending.Records.Add(record);
            }
        }
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
        var type = entity.GetType();

        var explicitProp = type.GetProperty("DataSubjectId");
        if (explicitProp is not null)
        {
            var value = explicitProp.GetValue(entity)?.ToString();
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    $"Entity '{type.Name}' declares 'DataSubjectId' but its value is null or empty at SaveChanges time. " +
                    "Audit records require a stable subject identifier — set DataSubjectId before persisting.");
            }
            return value;
        }

        // Fallback to Id / UserId only when no explicit DataSubjectId exists.
        // Reject the database-generated default value (0/empty Guid) that EF assigns before insert,
        // which would otherwise group unrelated rows under the same fake subject.
        var fallbackProp = type.GetProperty("Id") ?? type.GetProperty("UserId");
        var fallbackValue = fallbackProp?.GetValue(entity);
        var stringValue = fallbackValue?.ToString();

        if (string.IsNullOrEmpty(stringValue) || IsUnsetIdentifier(fallbackValue))
        {
            throw new InvalidOperationException(
                $"Entity '{type.Name}' has no resolvable DataSubjectId, and the fallback Id/UserId is unset (null/0/empty Guid). " +
                "Add a public DataSubjectId property and assign it before SaveChanges so the audit trail can correlate rows reliably.");
        }

        return stringValue;
    }

    private static bool IsUnsetIdentifier(object? value) => value switch
    {
        null      => true,
        int i     => i == 0,
        long l    => l == 0L,
        short s   => s == 0,
        byte b    => b == 0,
        Guid g    => g == Guid.Empty,
        _         => false,
    };

    private sealed class PendingAuditRecords
    {
        public List<AuditRecord> Records { get; } = [];
    }
}
