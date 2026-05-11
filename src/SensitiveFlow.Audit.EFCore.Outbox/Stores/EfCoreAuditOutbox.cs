using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Outbox.Entities;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.EFCore.Outbox.Stores;

/// <summary>
/// EF Core-backed durable audit outbox. Persists outbox entries in the same transaction
/// as the audit record for at-least-once delivery semantics with transactional guarantee.
/// </summary>
public sealed class EfCoreAuditOutbox : IDurableAuditOutbox
{
    private readonly IDbContextFactory<AuditDbContext> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes a new instance.</summary>
    public EfCoreAuditOutbox(IDbContextFactory<AuditDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entry = new AuditOutboxEntryEntity
        {
            AuditRecordId = record.Id.ToString(),
            Payload = JsonSerializer.Serialize(record, JsonOptions),
            EnqueuedAt = DateTimeOffset.UtcNow,
        };
        ctx.Set<AuditOutboxEntryEntity>().Add(entry);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditOutboxEntry>> DequeueBatchAsync(int max, CancellationToken cancellationToken = default)
    {
        if (max <= 0)
        {
            throw new ArgumentException("Must be > 0", nameof(max));
        }

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var pending = await ctx.Set<AuditOutboxEntryEntity>()
            .Where(e => !e.IsProcessed && !e.IsDeadLettered)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Order by EnqueuedAt on client side (SQLite doesn't support DateTimeOffset in ORDER BY)
        return pending
            .OrderBy(e => e.EnqueuedAt)
            .Take(max)
            .Select(ToOutboxEntry)
            .ToList();
    }

    /// <inheritdoc />
    public async Task MarkProcessedAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids?.ToList() ?? throw new ArgumentNullException(nameof(ids));
        if (idList.Count == 0)
        {
            return;
        }

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entries = await ctx.Set<AuditOutboxEntryEntity>()
            .Where(e => idList.Contains(e.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in entries)
        {
            entry.IsProcessed = true;
            entry.ProcessedAt = DateTimeOffset.UtcNow;
        }

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entry = await ctx.Set<AuditOutboxEntryEntity>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entry != null)
        {
            entry.Attempts++;
            entry.LastAttemptAt = DateTimeOffset.UtcNow;
            entry.LastError = error;
            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Mark an entry as dead-lettered after max retries.</summary>
    public async Task MarkDeadLetteredAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await ctx.Set<AuditOutboxEntryEntity>()
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.IsDeadLettered, true)
                    .SetProperty(e => e.DeadLetterReason, reason),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get pending entries count.</summary>
    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Set<AuditOutboxEntryEntity>()
            .Where(e => !e.IsProcessed && !e.IsDeadLettered)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Get dead-lettered entries for inspection.</summary>
    public async Task<IReadOnlyList<AuditOutboxEntry>> GetDeadLetteredAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var deadLettered = await ctx.Set<AuditOutboxEntryEntity>()
            .Where(e => e.IsDeadLettered)
            .OrderByDescending(e => e.EnqueuedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return deadLettered.Select(ToOutboxEntry).ToList();
    }

    private static AuditOutboxEntry ToOutboxEntry(AuditOutboxEntryEntity entity)
    {
        var record = JsonSerializer.Deserialize<AuditRecord>(entity.Payload, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize audit record from outbox entry {entity.Id}");

        return new AuditOutboxEntry
        {
            Id = entity.Id,
            Record = record,
            Attempts = entity.Attempts,
            EnqueuedAt = entity.EnqueuedAt,
            LastAttemptAt = entity.LastAttemptAt,
            LastError = entity.LastError,
        };
    }
}
