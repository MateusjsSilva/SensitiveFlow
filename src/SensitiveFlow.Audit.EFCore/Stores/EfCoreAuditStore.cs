using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Audit.EFCore.Entities;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.EFCore.Stores;

/// <summary>
/// EF Core-backed implementation of <see cref="IAuditStore"/> and <see cref="IBatchAuditStore"/>.
/// Persists audit records into the <typeparamref name="TContext"/>'s <c>DbSet&lt;AuditRecordEntity&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The store opens a fresh <typeparamref name="TContext"/> from <see cref="IDbContextFactory{TContext}"/>
/// for each call so that audit appends do not piggyback on the application's <see cref="DbContext"/>.
/// This is critical: appending into the application context would couple the audit transaction to the
/// caller's <c>SaveChanges</c> rollback semantics, which is precisely the failure mode the
/// <see cref="SensitiveFlow.Audit.Decorators.RetryingAuditStore"/> exists to avoid.
/// </para>
/// </remarks>
public sealed class EfCoreAuditStore<TContext> : IBatchAuditStore, IAuditStoreTransaction where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;
    private readonly Func<TContext, DbSet<AuditRecordEntity>> _setSelector;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="factory">Factory that produces fresh <typeparamref name="TContext"/> instances.</param>
    /// <param name="setSelector">
    /// Selector returning the <see cref="DbSet{TEntity}"/> on the context. Defaults to looking up the
    /// set via <see cref="DbContext.Set{TEntity}()"/> when not provided.
    /// </param>
    public EfCoreAuditStore(
        IDbContextFactory<TContext> factory,
        Func<TContext, DbSet<AuditRecordEntity>>? setSelector = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _setSelector = setSelector ?? (static ctx => ctx.Set<AuditRecordEntity>());
    }

    /// <inheritdoc />
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);
        set.Add(AuditRecordEntity.FromRecord(record));
        try
        {
            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw SchemaErrorTranslator.Translate(ex, typeof(TContext).Name);
        }
    }

    /// <inheritdoc />
    public async Task AppendRangeAsync(IReadOnlyCollection<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
        {
            return;
        }

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);
        set.AddRange(records.Select(AuditRecordEntity.FromRecord));
        try
        {
            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw SchemaErrorTranslator.Translate(ex, typeof(TContext).Name);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        ValidatePagination(skip, take);
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = _setSelector(ctx).AsNoTracking();
        if (from.HasValue) { query = query.Where(r => r.Timestamp >= from.Value); }
        if (to.HasValue)   { query = query.Where(r => r.Timestamp <= to.Value); }

        var rows = await query.OrderBy(r => r.Timestamp).Skip(skip).Take(take)
                              .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(static e => e.ToRecord());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataSubjectId);
        ValidatePagination(skip, take);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = _setSelector(ctx).AsNoTracking()
            .Where(r => r.DataSubjectId == dataSubjectId);

        if (from.HasValue) { query = query.Where(r => r.Timestamp >= from.Value); }
        if (to.HasValue)   { query = query.Where(r => r.Timestamp <= to.Value); }

        var rows = await query.OrderBy(r => r.Timestamp).Skip(skip).Take(take)
                              .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ConvertAll(static e => e.ToRecord());
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static void ValidatePagination(int skip, int take)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), skip, "skip must be non-negative.");
        }
        if (take < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, "take must be non-negative.");
        }
    }
}
