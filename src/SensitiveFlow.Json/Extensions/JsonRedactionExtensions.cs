using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Converters;

namespace SensitiveFlow.Json.Extensions;

/// <summary>
/// Extension methods that wire SensitiveFlow JSON redaction into <see cref="JsonSerializerOptions"/>
/// and into the DI container.
/// </summary>
public static class JsonRedactionExtensions
{
    /// <summary>
    /// Adds the SensitiveFlow redaction modifier to <paramref name="options"/>.
    /// Properties annotated with <c>[PersonalData]</c> or <c>[SensitiveData]</c> will be
    /// redacted according to <paramref name="redactionOptions"/>.
    /// </summary>
    public static JsonSerializerOptions WithSensitiveDataRedaction(
        this JsonSerializerOptions options,
        JsonRedactionOptions? redactionOptions = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var redaction = redactionOptions ?? new JsonRedactionOptions();
        var resolver = options.TypeInfoResolver as DefaultJsonTypeInfoResolver
                       ?? new DefaultJsonTypeInfoResolver();

        resolver.Modifiers.Add(SensitiveJsonModifier.Create(redaction));
        options.TypeInfoResolver = resolver;
        return options;
    }

    /// <summary>
    /// Registers <see cref="JsonRedactionOptions"/> in DI. Resolve via
    /// <see cref="IOptions{TOptions}"/> to apply the configuration to your serializer.
    /// </summary>
    public static IServiceCollection AddSensitiveFlowJsonRedaction(
        this IServiceCollection services,
        Action<JsonRedactionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<JsonRedactionOptions>();
        }

        return services;
    }
}
