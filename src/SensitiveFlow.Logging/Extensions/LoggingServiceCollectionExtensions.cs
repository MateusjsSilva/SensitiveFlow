using Microsoft.Extensions.DependencyInjection;
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
}
