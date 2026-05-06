using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Anonymization.Pseudonymizers;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Extensions;

/// <summary>
/// Extension methods for registering SensitiveFlow anonymization services.
/// </summary>
public static class AnonymizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers a durable <see cref="ITokenStore"/> implementation and wires
    /// <see cref="TokenPseudonymizer"/> as the scoped <see cref="IPseudonymizer"/>.
    /// </summary>
    /// <typeparam name="TStore">
    /// Your <see cref="ITokenStore"/> implementation backed by a durable sink
    /// (SQL, Redis, etc.).
    /// Token mappings must survive process restarts — losing them makes previously
    /// pseudonymized values irrecoverable.
    /// </typeparam>
    /// <example>
    /// <code>
    /// // SQL via EF Core:
    /// builder.Services.AddTokenStore&lt;EfCoreTokenStore&gt;();
    ///
    /// // Or register manually with a factory:
    /// builder.Services.AddScoped&lt;ITokenStore&gt;(sp =>
    ///     new EfCoreTokenStore(sp.GetRequiredService&lt;TokenDbContext&gt;()));
    /// builder.Services.AddScoped&lt;IPseudonymizer, TokenPseudonymizer&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddTokenStore<TStore>(this IServiceCollection services)
        where TStore : class, ITokenStore
    {
        services.AddScoped<ITokenStore, TStore>();
        services.AddScoped<IPseudonymizer, TokenPseudonymizer>();
        return services;
    }
}
