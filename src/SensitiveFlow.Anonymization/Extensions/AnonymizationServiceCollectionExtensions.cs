using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SensitiveFlow.Anonymization.Erasure;
using SensitiveFlow.Anonymization.Export;
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
    /// // Custom store backed by your own DbContext that maps TokenMappingEntity:
    /// builder.Services.AddTokenStore&lt;MyEfCoreTokenStore&gt;();
    ///
    /// // Or register both components manually:
    /// builder.Services.AddScoped&lt;ITokenStore, MyEfCoreTokenStore&gt;();
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

    /// <summary>
    /// Registers <see cref="DataSubjectErasureService"/> with the default
    /// <see cref="RedactionErasureStrategy"/> so application code can satisfy "right to be
    /// forgotten" requests by overwriting annotated properties on an entity. Replace the
    /// strategy registration to customize the replacement value.
    /// </summary>
    public static IServiceCollection AddDataSubjectErasure(this IServiceCollection services)
    {
        services.TryAddSingleton<IErasureStrategy>(new RedactionErasureStrategy());
        services.TryAddSingleton<IDataSubjectErasureService, DataSubjectErasureService>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="DataSubjectExporter"/> as the singleton <see cref="IDataSubjectExporter"/>
    /// so application code can satisfy data-portability requests by extracting annotated
    /// properties from any entity into a portable dictionary.
    /// </summary>
    public static IServiceCollection AddDataSubjectExport(this IServiceCollection services)
    {
        services.TryAddSingleton<IDataSubjectExporter, DataSubjectExporter>();
        return services;
    }
}
