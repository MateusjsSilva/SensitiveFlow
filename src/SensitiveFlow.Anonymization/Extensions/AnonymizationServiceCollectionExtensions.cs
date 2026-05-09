using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SensitiveFlow.Anonymization.Decorators;
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
    /// Registers a durable <see cref="ITokenStore"/> implementation.
    /// Does <b>not</b> register <see cref="IPseudonymizer"/> — call
    /// <see cref="AddTokenPseudonymizer"/> or <see cref="AddPseudonymizer{TPseudonymizer}"/>
    /// separately if you need reversible pseudonymization.
    /// </summary>
    /// <typeparam name="TStore">
    /// Your <see cref="ITokenStore"/> implementation backed by a durable sink
    /// (SQL, Redis, etc.).
    /// Token mappings must survive process restarts — losing them makes previously
    /// pseudonymized values irrecoverable.
    /// </typeparam>
    /// <example>
    /// <code>
    /// // Register the store and the pseudonymizer separately:
    /// builder.Services.AddTokenStore&lt;MyEfCoreTokenStore&gt;();
    /// builder.Services.AddTokenPseudonymizer();
    ///
    /// // Or use a different pseudonymizer:
    /// builder.Services.AddTokenStore&lt;MyEfCoreTokenStore&gt;();
    /// builder.Services.AddPseudonymizer&lt;HmacPseudonymizer&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddTokenStore<TStore>(this IServiceCollection services)
        where TStore : class, ITokenStore
    {
        services.AddScoped<ITokenStore, TStore>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="TokenPseudonymizer"/> as the scoped <see cref="IPseudonymizer"/>.
    /// Requires an <see cref="ITokenStore"/> to already be registered.
    /// </summary>
    public static IServiceCollection AddTokenPseudonymizer(this IServiceCollection services)
    {
        services.AddScoped<IPseudonymizer, TokenPseudonymizer>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IPseudonymizer"/> implementation.
    /// Use this when you want a non-default pseudonymizer (e.g. <c>HmacPseudonymizer</c>)
    /// instead of <see cref="TokenPseudonymizer"/>.
    /// </summary>
    /// <typeparam name="TPseudonymizer">Your <see cref="IPseudonymizer"/> implementation.</typeparam>
    public static IServiceCollection AddPseudonymizer<TPseudonymizer>(this IServiceCollection services)
        where TPseudonymizer : class, IPseudonymizer
    {
        services.AddScoped<IPseudonymizer, TPseudonymizer>();
        return services;
    }

    /// <summary>
    /// Wraps the registered <see cref="ITokenStore"/> with <see cref="CachingTokenStore"/>
    /// to avoid repeated roundtrips for hot pseudonymization mappings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for cache options.</param>
    /// <remarks>
    /// Call this <b>after</b> <see cref="AddTokenStore{TStore}"/>. The cache is in-process and
    /// stores original values in memory, so choose the size according to the application's
    /// memory exposure requirements.
    /// </remarks>
    public static IServiceCollection AddCachingTokenStore(
        this IServiceCollection services,
        Action<CachingTokenStoreOptions>? configure = null)
    {
        var options = new CachingTokenStoreOptions();
        configure?.Invoke(options);

        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(ITokenStore))
            ?? throw new InvalidOperationException(
                $"No {nameof(ITokenStore)} registration was found. Call AddTokenStore<T>() before AddCachingTokenStore().");

        services.Remove(existing);

        if (existing.ImplementationType is not null)
        {
            services.Add(new ServiceDescriptor(existing.ImplementationType, existing.ImplementationType, existing.Lifetime));
            services.Add(new ServiceDescriptor(typeof(ITokenStore),
                sp => new CachingTokenStore(
                    (ITokenStore)sp.GetRequiredService(existing.ImplementationType),
                    options),
                existing.Lifetime));
        }
        else if (existing.ImplementationFactory is not null)
        {
            var factory = existing.ImplementationFactory;
            services.Add(new ServiceDescriptor(typeof(ITokenStore),
                sp => new CachingTokenStore((ITokenStore)factory(sp), options),
                existing.Lifetime));
        }
        else if (existing.ImplementationInstance is ITokenStore instance)
        {
            services.AddSingleton<ITokenStore>(new CachingTokenStore(instance, options));
        }

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
