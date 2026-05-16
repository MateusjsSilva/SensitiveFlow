using Microsoft.EntityFrameworkCore;
using SensitiveFlow.TokenStore.EFCore.Entities;

namespace SensitiveFlow.TokenStore.EFCore.KeyRotation;

/// <summary>
/// Service for managing token key rotation and migration.
/// Enables bulk updates to token mappings when the pseudonymization scheme changes.
/// </summary>
/// <typeparam name="TContext">The EF Core <see cref="DbContext"/> type.</typeparam>
public sealed class TokenKeyRotationService<TContext> where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;
    private readonly Func<TContext, DbSet<TokenMappingEntity>> _setSelector;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenKeyRotationService{TContext}"/> class.
    /// </summary>
    /// <param name="factory">Factory that produces fresh <typeparamref name="TContext"/> instances.</param>
    /// <param name="setSelector">
    /// Selector returning the <see cref="DbSet{TEntity}"/> on the context. Defaults to looking up the
    /// set via <see cref="DbContext.Set{TEntity}()"/> when not provided.
    /// </param>
    public TokenKeyRotationService(
        IDbContextFactory<TContext> factory,
        Func<TContext, DbSet<TokenMappingEntity>>? setSelector = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _setSelector = setSelector ?? (static ctx => ctx.Set<TokenMappingEntity>());
    }

    /// <summary>
    /// Asynchronously retrieves all token mappings from the database.
    /// Useful for inspection and planning bulk migrations.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of all token mappings.</returns>
    public async Task<IReadOnlyList<TokenMappingEntity>> GetAllTokensAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);

        var tokens = await set
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return tokens.AsReadOnly();
    }

    /// <summary>
    /// Asynchronously replaces the token value for a mapping identified by ID.
    /// </summary>
    /// <param name="id">The primary key of the mapping to update.</param>
    /// <param name="newToken">The new token value.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    public async Task ReplaceTokenAsync(long id, string newToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newToken);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);

        var mapping = await set.FindAsync(new object[] { id }, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (mapping is null)
        {
            throw new KeyNotFoundException($"Token mapping with ID {id} not found.");
        }

        mapping.Token = newToken;
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously updates multiple token mappings in a single batch operation.
    /// </summary>
    /// <param name="updates">An enumerable of tuples containing the ID and new token for each mapping.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    public async Task BulkReplaceAsync(IEnumerable<(long id, string newToken)> updates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);

        var updateList = updates.ToList();
        if (updateList.Count == 0)
        {
            return;
        }

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);

        foreach (var (id, newToken) in updateList)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newToken);

            var mapping = await set.FindAsync(new object[] { id }, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (mapping is null)
            {
                throw new KeyNotFoundException($"Token mapping with ID {id} not found.");
            }

            mapping.Token = newToken;
        }

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously deletes a token mapping by ID.
    /// </summary>
    /// <param name="id">The primary key of the mapping to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);

        var mapping = await set.FindAsync(new object[] { id }, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (mapping is null)
        {
            throw new KeyNotFoundException($"Token mapping with ID {id} not found.");
        }

        set.Remove(mapping);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
