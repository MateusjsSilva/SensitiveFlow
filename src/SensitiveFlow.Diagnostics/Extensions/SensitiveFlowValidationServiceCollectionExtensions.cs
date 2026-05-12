using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Exceptions;
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

    /// <summary>
    /// Runs SensitiveFlow startup validation from a built service provider.
    /// </summary>
    /// <remarks>
    /// When the resolved <see cref="SensitiveFlowValidationOptions.FailOnError"/> is <c>true</c>
    /// (the default), this throws <see cref="SensitiveFlowConfigurationException"/> on any
    /// <c>Error</c> diagnostic. Warnings never throw — they are returned via the report so
    /// the caller can log them.
    /// </remarks>
    public static SensitiveFlowConfigurationReport ValidateSensitiveFlow(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.GetService<SensitiveFlowValidationOptions>()
            ?? new SensitiveFlowValidationOptions
            {
                RequireAuditStore = true,
                RequireTokenStore = false,
                RequireJsonRedaction = false,
                RequireRetention = false,
            };

        var validator = services.GetService<SensitiveFlowConfigurationValidator>()
            ?? new SensitiveFlowConfigurationValidator(options);

        var report = validator.Validate(services);

        if (options.FailOnError && !report.IsValid)
        {
            var errors = report.Diagnostics
                .Where(d => d.Severity == SensitiveFlowDiagnosticSeverity.Error)
                .Select(d => $"  [{d.Code}] {d.Message}");

            var message =
                "SensitiveFlow startup validation failed with one or more errors:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, errors) +
                Environment.NewLine +
                "Resolve them, or set SensitiveFlowValidationOptions.FailOnError = false to downgrade to warnings.";

            throw new SensitiveFlowConfigurationException(message, "SF-CONFIG-FAIL");
        }

        return report;
    }
}
