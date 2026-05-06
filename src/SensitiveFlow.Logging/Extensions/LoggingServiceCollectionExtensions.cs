using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    /// <param name="marker">The redaction marker. Defaults to <c>[REDACTED]</c>.</param>
    public static IServiceCollection AddSensitiveFlowLogging(
        this IServiceCollection services,
        string marker = "[REDACTED]")
    {
        services.AddSingleton<ISensitiveValueRedactor>(new DefaultSensitiveValueRedactor(marker));
        return services;
    }

    /// <summary>
    /// Registers SensitiveFlow redaction for an inner logging provider.
    /// </summary>
    /// <typeparam name="TProvider">The provider to wrap with <see cref="RedactingLoggerProvider"/>.</typeparam>
    /// <param name="builder">The logging builder.</param>
    /// <param name="marker">The redaction marker. Defaults to <c>[REDACTED]</c>.</param>
    public static ILoggingBuilder AddSensitiveFlowLogging<TProvider>(
        this ILoggingBuilder builder,
        string marker = "[REDACTED]")
        where TProvider : class, ILoggerProvider
    {
        builder.Services.AddSensitiveFlowLogging(marker);
        builder.Services.AddSingleton<ILoggerProvider>(sp =>
            new RedactingLoggerProvider(
                ActivatorUtilities.CreateInstance<TProvider>(sp),
                sp.GetRequiredService<ISensitiveValueRedactor>()));

        return builder;
    }
}
