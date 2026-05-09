using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Audit.Snapshots.EFCore.Entities;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Snapshots.EFCore.Stores;

/// <summary>
/// EF Core-backed implementation of <see cref="IAuditSnapshotStore"/>.
/// Persists aggregate-level audit snapshots into the <typeparamref name="TContext"/>'s
/// <c>DbSet&lt;AuditSnapshotEntity&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The store opens a fresh <typeparamref name="TContext"/> from <see cref="IDbContextFactory{TContext}"/>
/// for each call so that snapshot appends do not piggyback on the application's <see cref="DbContext"/>.
/// </para>
/// </remarks>
public sealed class EfCoreAuditSnapshotStore<TContext> : IAuditSnapshotStore where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;
    private readonly Func<TContext, DbSet<AuditSnapshotEntity>> _setSelector;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="factory">Factory that produces fresh <typeparamref name="TContext"/> instances.</param>
    /// <param name="setSelector">
    /// Selector returning the <see cref="DbSet{TEntity}"/> on the context. Defaults to looking up the
    /// set via <see cref="DbContext.Set{TEntity}()"/> when not provided.
    /// </param>
    public EfCoreAuditSnapshotStore(
        IDbContextFactory<TContext> factory,
        Func<TContext, DbSet<AuditSnapshotEntity>>? setSelector = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _setSelector = setSelector ?? (static ctx => ctx.Set<AuditSnapshotEntity>());
    }

    /// <inheritdoc />
    public async Task AppendAsync(AuditSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);
        set.Add(AuditSnapshotEntity.FromSnapshot(snapshot));
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditSnapshot>> QueryByAggregateAsync(
        string aggregate,
        string aggregateId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregate);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = _setSelector(ctx).AsNoTracking()
            .Where(s => s.Aggregate == aggregate && s.AggregateId == aggregateId);

        if (from.HasValue) { query = query.Where(s => s.Timestamp >= from.Value); }
        if (to.HasValue)   { query = query.Where(s => s.Timestamp <= to.Value); }

        var rows = await query.OrderBy(s => s.Timestamp).Skip(skip).Take(take)
                              .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(static e => e.ToSnapshot());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditSnapshot>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = _setSelector(ctx).AsNoTracking()
            .Where(s => s.DataSubjectId == dataSubjectId);

        if (from.HasValue) { query = query.Where(s => s.Timestamp >= from.Value); }
        if (to.HasValue)   { query = query.Where(s => s.Timestamp <= to.Value); }

        var rows = await query.OrderBy(s => s.Timestamp).Skip(skip).Take(take)
                              .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(static e => e.ToSnapshot());
    }
}
