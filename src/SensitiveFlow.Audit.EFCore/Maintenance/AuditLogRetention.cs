using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Audit.EFCore.Entities;

namespace SensitiveFlow.Audit.EFCore.Maintenance;

/// <summary>
/// Maintenance helper that purges audit records older than a given retention horizon. The audit
/// log itself accumulates personal data over time (subject IDs, actor IDs) and falls under the
/// same retention duty as the data it audits.
/// </summary>
public sealed class AuditLogRetention<TContext> : IAuditLogRetention where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;
    private readonly Func<TContext, DbSet<AuditRecordEntity>> _setSelector;

    /// <summary>Initializes a new instance.</summary>
    public AuditLogRetention(
        IDbContextFactory<TContext> factory,
        Func<TContext, DbSet<AuditRecordEntity>>? setSelector = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _setSelector = setSelector ?? (static ctx => ctx.Set<AuditRecordEntity>());
    }

    /// <summary>
    /// Deletes audit records whose <see cref="AuditRecordEntity.Timestamp"/> is older than
    /// <paramref name="olderThan"/>. Returns the number of rows deleted.
    /// </summary>
    /// <remarks>
    /// Uses <c>ExecuteDeleteAsync</c> so the deletion runs as a single SQL statement on relational
    /// providers without materializing the rows on the client.
    /// </remarks>
    public async Task<int> PurgeOlderThanAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = _setSelector(ctx)
            .Where(r => r.Timestamp < olderThan);

        try
        {
            return await query.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            return await FallbackDeleteAsync(ctx, query, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<int> FallbackDeleteAsync(
        DbContext ctx,
        IQueryable<AuditRecordEntity> query,
        CancellationToken cancellationToken)
    {
        var rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return 0;
        }

        ctx.RemoveRange(rows);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }
}
