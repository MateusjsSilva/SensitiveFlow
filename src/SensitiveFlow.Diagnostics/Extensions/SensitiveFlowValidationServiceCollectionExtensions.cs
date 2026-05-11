using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Diagnostics.Validation;

namespace SensitiveFlow.Diagnostics.Extensions;

/// <summary>
/// Service registration helpers for SensitiveFlow configuration validation.
/// </summary>
public static class SensitiveFlowValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared SensitiveFlow options object used by policies,
    /// profiles, and diagnostics.
    /// </summary>
    public static IServiceCollection AddSensitiveFlow(
        this IServiceCollection services,
        Action<SensitiveFlowOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new SensitiveFlowOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services;
    }

    /// <summary>Registers startup validation options and validator services.</summary>
    public static IServiceCollection AddSensitiveFlowValidation(
        this IServiceCollection services,
        Action<SensitiveFlowValidationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new SensitiveFlowValidationOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<SensitiveFlowConfigurationValidator>();
        return services;
    }

    /// <summary>Runs SensitiveFlow startup validation from a built service provider.</summary>
    public static SensitiveFlowConfigurationReport ValidateSensitiveFlow(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var validator = services.GetService<SensitiveFlowConfigurationValidator>()
            ?? new SensitiveFlowConfigurationValidator(new SensitiveFlowValidationOptions
            {
                RequireAuditStore = true,
                RequireTokenStore = false,
                RequireJsonRedaction = false,
                RequireRetention = false,
            });

        return validator.Validate(services);
    }
}
