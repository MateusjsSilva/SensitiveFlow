using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Profiles;

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

        var sensitiveFlowOptions = provider.GetService<SensitiveFlowOptions>();
        if (sensitiveFlowOptions is not null)
        {
            AddPolicyDiagnostics(provider, sensitiveFlowOptions, diagnostics);
        }

        return new SensitiveFlowConfigurationReport(diagnostics);
    }

    private static void AddPolicyDiagnostics(
        IServiceProvider provider,
        SensitiveFlowOptions options,
        ICollection<SensitiveFlowConfigurationDiagnostic> diagnostics)
    {
        if (options.Policies.Rules.Any(static r => HasAction(r, SensitiveFlowPolicyAction.RequireAudit))
            && provider.GetService<IAuditStore>() is null)
        {
            diagnostics.Add(Warning("SF-CONFIG-006", "SensitiveFlow policies require audit support, but no IAuditStore registration was found."));
        }

        if (options.Policies.Rules.Any(static r => HasAction(r, SensitiveFlowPolicyAction.RedactInJson | SensitiveFlowPolicyAction.OmitInJson))
            && !HasRegisteredOptions(provider, "SensitiveFlow.Json.Configuration.JsonRedactionOptions, SensitiveFlow.Json"))
        {
            diagnostics.Add(Warning("SF-CONFIG-007", "SensitiveFlow policies configure JSON redaction, but JsonRedactionOptions were not found."));
        }

        if (options.Policies.Rules.Any(static r => HasAction(r, SensitiveFlowPolicyAction.MaskInLogs))
            && !HasRegisteredType(provider, "SensitiveFlow.Logging.Redaction.ISensitiveValueRedactor, SensitiveFlow.Logging"))
        {
            diagnostics.Add(Warning("SF-CONFIG-008", "SensitiveFlow policies configure log masking, but ISensitiveValueRedactor was not found."));
        }
    }

    private static bool HasAction(SensitiveFlowPolicyRule rule, SensitiveFlowPolicyAction action)
        => (rule.Actions & action) != SensitiveFlowPolicyAction.None;

    private static bool HasRegisteredType(IServiceProvider provider, string assemblyQualifiedTypeName)
    {
        var type = Type.GetType(assemblyQualifiedTypeName);
        return type is not null && provider.GetService(type) is not null;
    }

    private static bool HasRegisteredOptions(IServiceProvider provider, string assemblyQualifiedTypeName)
    {
        var type = Type.GetType(assemblyQualifiedTypeName);
        if (type is null)
        {
            return false;
        }

        var optionsType = typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(type);
        return provider.GetService(optionsType) is not null;
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
