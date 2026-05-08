using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Diagnostics.Decorators;

namespace SensitiveFlow.Diagnostics.Extensions;

/// <summary>DI extensions that wire the SensitiveFlow diagnostics decorators.</summary>
public static class DiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Wraps the registered <see cref="IAuditStore"/> with <see cref="InstrumentedAuditStore"/>
    /// so spans and metrics are emitted via <c>System.Diagnostics</c>. Pair with the
    /// <c>SensitiveFlow</c> ActivitySource/Meter in your OpenTelemetry registration.
    /// </summary>
    /// <remarks>
    /// Apply <b>after</b> any retry decorator if you want spans to capture the entire retry
    /// cycle as a single logical operation; apply <b>before</b> the retry decorator (closer to
    /// the durable store) if you want one span per attempt.
    /// </remarks>
    public static IServiceCollection AddSensitiveFlowDiagnostics(this IServiceCollection services)
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore))
            ?? throw new InvalidOperationException(
                $"No {nameof(IAuditStore)} registration was found. Call AddAuditStore<T>() before AddSensitiveFlowDiagnostics().");

        services.Remove(existing);

        if (existing.ImplementationType is not null)
        {
            services.Add(new ServiceDescriptor(existing.ImplementationType, existing.ImplementationType, existing.Lifetime));
            services.Add(new ServiceDescriptor(typeof(IAuditStore), sp => new InstrumentedAuditStore(
                (IAuditStore)sp.GetRequiredService(existing.ImplementationType)), existing.Lifetime));
        }
        else if (existing.ImplementationFactory is not null)
        {
            var factory = existing.ImplementationFactory;
            services.Add(new ServiceDescriptor(typeof(IAuditStore),
                sp => new InstrumentedAuditStore((IAuditStore)factory(sp)), existing.Lifetime));
        }
        else if (existing.ImplementationInstance is IAuditStore instance)
        {
            services.AddSingleton<IAuditStore>(new InstrumentedAuditStore(instance));
        }

        return services;
    }
}
