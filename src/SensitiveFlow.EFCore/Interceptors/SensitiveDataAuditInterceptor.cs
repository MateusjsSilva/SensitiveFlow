using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

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
    /// <b>Deadlock warning:</b> this override blocks the calling thread via
    /// <c>GetAwaiter().GetResult()</c>. In ASP.NET Core the thread-pool synchronization
    /// context makes blocking on async code unsafe under high concurrency.
    /// Prefer <see cref="SavingChangesAsync"/> — use <c>DbContext.SaveChangesAsync</c>
    /// instead of <c>SaveChanges</c> in all application code.
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
            var entityName = entityType.Name;
            var operation = MapOperation(entry.State);

            var dataSubjectId = ResolveDataSubjectId(entry.Entity);

            foreach (var property in entityType.GetProperties())
            {
                var isPersonal = Attribute.IsDefined(property, typeof(PersonalDataAttribute));
                var isSensitive = Attribute.IsDefined(property, typeof(SensitiveDataAttribute));

                if (!isPersonal && !isSensitive)
                {
                    continue;
                }

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
        var idProp = entity.GetType().GetProperty("DataSubjectId")
                  ?? entity.GetType().GetProperty("Id")
                  ?? entity.GetType().GetProperty("UserId");

        return idProp?.GetValue(entity)?.ToString() ?? "unknown";
    }

    private sealed class PendingAuditRecords
    {
        public List<AuditRecord> Records { get; } = [];
    }
}
