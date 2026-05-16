using Microsoft.EntityFrameworkCore;
using SensitiveFlow.TokenStore.EFCore.Entities;

namespace SensitiveFlow.TokenStore.EFCore.Expiration;

/// <summary>
/// Service for managing token expiration and cleanup.
/// Enables purging of expired tokens from the database based on their creation time.
/// </summary>
/// <typeparam name="TContext">The EF Core <see cref="DbContext"/> type.</typeparam>
public sealed class TokenExpirationService<TContext> where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;
    private readonly Func<TContext, DbSet<TokenMappingEntity>> _setSelector;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenExpirationService{TContext}"/> class.
    /// </summary>
    /// <param name="factory">Factory that produces fresh <typeparamref name="TContext"/> instances.</param>
    /// <param name="setSelector">
    /// Selector returning the <see cref="DbSet{TEntity}"/> on the context. Defaults to looking up the
    /// set via <see cref="DbContext.Set{TEntity}()"/> when not provided.
    /// </param>
    public TokenExpirationService(
        IDbContextFactory<TContext> factory,
        Func<TContext, DbSet<TokenMappingEntity>>? setSelector = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _setSelector = setSelector ?? (static ctx => ctx.Set<TokenMappingEntity>());
    }

    /// <summary>
    /// Asynchronously deletes all expired token mappings where <see cref="TokenMappingEntity.ExpiresAt"/>
    /// is less than the current UTC time.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The number of expired tokens deleted.</returns>
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);

        var now = DateTimeOffset.UtcNow;
        var all = await set
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var expired = all.Where(e => e.ExpiresAt != null && e.ExpiresAt < now).ToList();

        if (expired.Count == 0)
        {
            return 0;
        }

        set.RemoveRange(expired);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return expired.Count;
    }

    /// <summary>
    /// Asynchronously counts the number of expired token mappings without deleting them.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The count of expired tokens.</returns>
    public async Task<int> GetExpiredCountAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);

        var now = DateTimeOffset.UtcNow;
        var all = await set
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var count = all.Count(e => e.ExpiresAt != null && e.ExpiresAt < now);

        return count;
    }
}
