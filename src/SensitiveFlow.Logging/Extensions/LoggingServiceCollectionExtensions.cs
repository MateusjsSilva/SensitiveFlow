using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Logging.Configuration;
using SensitiveFlow.Logging.Loggers;
using SensitiveFlow.Logging.Redaction;

namespace SensitiveFlow.Logging.Extensions;

/// <summary>
/// Extension methods for registering SensitiveFlow logging services.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="DefaultSensitiveValueRedactor"/> as <see cref="ISensitiveValueRedactor"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="marker">The redaction marker. Defaults to <see cref="SensitiveFlowDefaults.RedactedPlaceholder"/>.</param>
    public static IServiceCollection AddSensitiveFlowLogging(
        this IServiceCollection services,
        string marker = SensitiveFlowDefaults.RedactedPlaceholder)
        => services.AddSensitiveFlowLogging(options => options.RedactedPlaceholder = marker);

    /// <summary>
    /// Registers SensitiveFlow logging redaction with explicit options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional logging-redaction configuration.</param>
    public static IServiceCollection AddSensitiveFlowLogging(
        this IServiceCollection services,
        Action<SensitiveLoggingOptions>? configure)
    {
        var options = new SensitiveLoggingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ISensitiveValueRedactor>(new DefaultSensitiveValueRedactor(options.RedactedPlaceholder));
        return services;
    }

    /// <summary>
    /// Registers SensitiveFlow redaction for an inner logging provider.
    /// </summary>
    /// <typeparam name="TProvider">The provider to wrap with <see cref="RedactingLoggerProvider"/>.</typeparam>
    /// <param name="builder">The logging builder.</param>
    /// <param name="marker">The redaction marker. Defaults to <see cref="SensitiveFlowDefaults.RedactedPlaceholder"/>.</param>
    public static ILoggingBuilder AddSensitiveFlowLogging<TProvider>(
        this ILoggingBuilder builder,
        string marker = SensitiveFlowDefaults.RedactedPlaceholder)
        where TProvider : class, ILoggerProvider
        => builder.AddSensitiveFlowLogging<TProvider>(options => options.RedactedPlaceholder = marker);

    /// <summary>
    /// Registers SensitiveFlow redaction for an inner logging provider with explicit options.
    /// </summary>
    /// <typeparam name="TProvider">The provider to wrap with <see cref="RedactingLoggerProvider"/>.</typeparam>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">Optional logging-redaction configuration.</param>
    public static ILoggingBuilder AddSensitiveFlowLogging<TProvider>(
        this ILoggingBuilder builder,
        Action<SensitiveLoggingOptions>? configure)
        where TProvider : class, ILoggerProvider
    {
        builder.Services.AddSensitiveFlowLogging(configure);
        builder.Services.AddSingleton<ILoggerProvider>(sp =>
            new RedactingLoggerProvider(
                ActivatorUtilities.CreateInstance<TProvider>(sp),
                sp.GetRequiredService<ISensitiveValueRedactor>(),
                sp.GetRequiredService<SensitiveLoggingOptions>()));

        return builder;
    }
}
