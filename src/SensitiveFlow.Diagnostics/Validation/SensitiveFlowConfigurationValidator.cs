using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Diagnostics.Validation;

/// <summary>
/// Validates SensitiveFlow service registrations at startup.
/// </summary>
public sealed class SensitiveFlowConfigurationValidator
{
    private readonly SensitiveFlowValidationOptions _options;

    /// <summary>Initializes a new instance.</summary>
    public SensitiveFlowConfigurationValidator(SensitiveFlowValidationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Validates a built service provider.</summary>
    public SensitiveFlowConfigurationReport Validate(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var diagnostics = new List<SensitiveFlowConfigurationDiagnostic>();
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        if (_options.RequireAuditStore && provider.GetService<IAuditStore>() is null)
        {
            diagnostics.Add(Warning("SF-CONFIG-001", "No IAuditStore registration was found."));
        }

        if (_options.RequireTokenStore && provider.GetService<ITokenStore>() is null)
        {
            diagnostics.Add(Warning("SF-CONFIG-002", "No durable ITokenStore registration was found."));
        }

        if (provider.GetService<IPseudonymizer>() is not null && provider.GetService<ITokenStore>() is null)
        {
            diagnostics.Add(Warning("SF-CONFIG-003", "IPseudonymizer is registered without an ITokenStore; reversible pseudonymization may not be durable."));
        }

        if (_options.RequireJsonRedaction && services.GetService(typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(Type.GetType("SensitiveFlow.Json.Configuration.JsonRedactionOptions, SensitiveFlow.Json") ?? typeof(object))) is null)
        {
            diagnostics.Add(Warning("SF-CONFIG-004", "JSON redaction is required but JsonRedactionOptions were not found."));
        }

        if (_options.RequireRetention && !services.GetServices<object>().Any(static s => s.GetType().FullName?.Contains("Retention", StringComparison.Ordinal) == true))
        {
            diagnostics.Add(Warning("SF-CONFIG-005", "Retention validation was requested, but no retention services were detected."));
        }

        return new SensitiveFlowConfigurationReport(diagnostics);
    }

    private static SensitiveFlowConfigurationDiagnostic Warning(string code, string message)
    {
        return new SensitiveFlowConfigurationDiagnostic
        {
            Code = code,
            Message = message,
            Severity = SensitiveFlowDiagnosticSeverity.Warning,
        };
    }
}

