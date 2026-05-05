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
            await EmitAuditRecordsAsync(eventData.Context, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            EmitAuditRecordsAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();

        return base.SavingChanges(eventData, result);
    }

    private async Task EmitAuditRecordsAsync(DbContext context, CancellationToken cancellationToken)
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
            var operation = entry.State switch
            {
                EntityState.Added => AuditOperation.Create,
                EntityState.Modified => AuditOperation.Update,
                EntityState.Deleted => AuditOperation.Delete,
                _ => AuditOperation.Access
            };

            var dataSubjectId = ResolveDataSubjectId(entry.Entity);

            foreach (var property in entityType.GetProperties())
            {
                var isPersonal = Attribute.IsDefined(property, typeof(PersonalDataAttribute));
                var isSensitive = Attribute.IsDefined(property, typeof(SensitiveDataAttribute));

                if (!isPersonal && !isSensitive)
                    continue;

                if (entry.State == EntityState.Modified)
                {
                    var propEntry = entry.Property(property.Name);
                    if (!propEntry.IsModified)
                        continue;
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

                await _auditStore.AppendAsync(record, cancellationToken);
            }
        }
    }

    private static string ResolveDataSubjectId(object entity)
    {
        var idProp = entity.GetType().GetProperty("DataSubjectId")
                  ?? entity.GetType().GetProperty("Id")
                  ?? entity.GetType().GetProperty("UserId");

        return idProp?.GetValue(entity)?.ToString() ?? "unknown";
    }
}
