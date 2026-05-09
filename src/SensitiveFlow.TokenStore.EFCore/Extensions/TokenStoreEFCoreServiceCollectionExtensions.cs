using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.TokenStore.EFCore.Stores;

namespace SensitiveFlow.TokenStore.EFCore.Extensions;

/// <summary>DI extensions for the EF Core token store.</summary>
public static class TokenStoreEFCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EfCoreTokenStore{TContext}"/> as the durable <see cref="ITokenStore"/>
    /// implementation, backed by a dedicated <see cref="TokenDbContext"/>.
    /// Also wires <see cref="TokenPseudonymizer"/> as the scoped <see cref="IPseudonymizer"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">Configures <see cref="DbContextOptionsBuilder"/> (provider, connection, etc.).</param>
    /// <example>
    /// <code>
    /// builder.Services.AddEfCoreTokenStore(opt =>
    ///     opt.UseSqlite(builder.Configuration.GetConnectionString("Tokens")));
    /// </code>
    /// </example>
    public static IServiceCollection AddEfCoreTokenStore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction)
    {
        ArgumentNullException.ThrowIfNull(optionsAction);

        services.AddDbContextFactory<TokenDbContext>(optionsAction);
        services.AddSingleton<ITokenStore>(sp =>
            new EfCoreTokenStore<TokenDbContext>(
                sp.GetRequiredService<IDbContextFactory<TokenDbContext>>(),
                static ctx => ctx.TokenMappings));
        services.AddScoped<IPseudonymizer, TokenPseudonymizer>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="EfCoreTokenStore{TContext}"/> against an existing user-owned
    /// <typeparamref name="TContext"/> that already has the <c>TokenMappingEntity</c> mapped.
    /// Also wires <see cref="TokenPseudonymizer"/> as the scoped <see cref="IPseudonymizer"/>.
    /// </summary>
    /// <typeparam name="TContext">Your application's <see cref="DbContext"/> that maps <c>TokenMappingEntity</c>.</typeparam>
    /// <example>
    /// <code>
    /// builder.Services.AddEfCoreTokenStore&lt;MyAppDbContext&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddEfCoreTokenStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddSingleton<ITokenStore>(sp =>
            new EfCoreTokenStore<TContext>(
                sp.GetRequiredService<IDbContextFactory<TContext>>()));
        services.AddScoped<IPseudonymizer, TokenPseudonymizer>();

        return services;
    }
}
