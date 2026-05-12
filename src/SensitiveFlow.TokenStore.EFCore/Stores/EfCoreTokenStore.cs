using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.TokenStore.EFCore.Entities;

namespace SensitiveFlow.TokenStore.EFCore.Stores;

/// <summary>
/// EF Core-backed implementation of <see cref="ITokenStore"/>.
/// Persists token ↔ value mappings into the <typeparamref name="TContext"/>'s
/// <c>DbSet&lt;TokenMappingEntity&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The store opens a fresh <typeparamref name="TContext"/> from <see cref="IDbContextFactory{TContext}"/>
/// for each call so that token creation does not piggyback on the application's <see cref="DbContext"/>.
/// </para>
/// <para>
/// <b>Concurrency:</b> <c>GetOrCreateTokenAsync</c> uses a unique index on <c>Value</c>.
/// When two callers race for the same value, one succeeds and the other catches
/// <see cref="DbUpdateException"/>, then recovers by reading the winner's token.
/// This is the same pattern used in the official samples.
/// </para>
/// </remarks>
public sealed class EfCoreTokenStore<TContext> : ITokenStore where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;
    private readonly Func<TContext, DbSet<TokenMappingEntity>> _setSelector;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="factory">Factory that produces fresh <typeparamref name="TContext"/> instances.</param>
    /// <param name="setSelector">
    /// Selector returning the <see cref="DbSet{TEntity}"/> on the context. Defaults to looking up the
    /// set via <see cref="DbContext.Set{TEntity}()"/> when not provided.
    /// </param>
    public EfCoreTokenStore(
        IDbContextFactory<TContext> factory,
        Func<TContext, DbSet<TokenMappingEntity>>? setSelector = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _setSelector = setSelector ?? (static ctx => ctx.Set<TokenMappingEntity>());
    }

    /// <inheritdoc />
    public async Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);

        TokenMappingEntity? existing;
        try
        {
            existing = await set
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Value == value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw SchemaErrorTranslator.Translate(ex, typeof(TContext).Name);
        }

        if (existing is not null)
        {
            return existing.Token;
        }

        var token = Guid.NewGuid().ToString("N");
        set.Add(new TokenMappingEntity { Value = value, Token = token });

        try
        {
            await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return token;
        }
        catch (DbUpdateException)
        {
            // Race: another caller inserted the same value between our read and write.
            // Detach our losing entity and read the winner's token.
            var local = set.Local.FirstOrDefault(t => t.Value == value);
            if (local is not null)
            {
                ctx.Entry(local).State = EntityState.Detached;
            }

            var winner = await set
                .AsNoTracking()
                .FirstAsync(t => t.Value == value, cancellationToken)
                .ConfigureAwait(false);
            return winner.Token;
        }
    }

    /// <inheritdoc />
    public async Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = _setSelector(ctx);

        TokenMappingEntity? mapping;
        try
        {
            mapping = await set
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Token == token, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw SchemaErrorTranslator.Translate(ex, typeof(TContext).Name);
        }

        return mapping?.Value
            ?? throw new KeyNotFoundException($"Token '{token}' not found in the store.");
    }
}
