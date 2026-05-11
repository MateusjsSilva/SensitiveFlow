using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SensitiveFlow.Core.Discovery;
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

        AddEfCoreDiagnostics(provider, diagnostics);
        AddRetentionDiagnostics(provider, diagnostics);
        AddAspNetCoreDiagnostics(provider, diagnostics);
        AddOutboxDiagnostics(provider, diagnostics);

        var sensitiveFlowOptions = provider.GetService<SensitiveFlowOptions>();
        if (sensitiveFlowOptions is not null)
        {
            AddPolicyDiagnostics(provider, sensitiveFlowOptions, diagnostics);
        }

        return new SensitiveFlowConfigurationReport(diagnostics);
    }

    private static void AddEfCoreDiagnostics(
        IServiceProvider provider,
        ICollection<SensitiveFlowConfigurationDiagnostic> diagnostics)
    {
        var interceptorType = Type.GetType("SensitiveFlow.EFCore.Interceptors.SensitiveDataAuditInterceptor, SensitiveFlow.EFCore");
        if (interceptorType is null)
        {
            return;
        }

        try
        {
            if (provider.GetService(interceptorType) is not null && provider.GetService<IAuditStore>() is null)
            {
                diagnostics.Add(Warning("SF-CONFIG-009", "SensitiveDataAuditInterceptor is registered, but no IAuditStore registration was found."));
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(nameof(IAuditStore), StringComparison.Ordinal))
        {
            diagnostics.Add(Warning("SF-CONFIG-009", "SensitiveDataAuditInterceptor is registered, but no IAuditStore registration was found."));
        }
    }

    private static void AddRetentionDiagnostics(
        IServiceProvider provider,
        ICollection<SensitiveFlowConfigurationDiagnostic> diagnostics)
    {
        var hasRetentionAnnotations = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static a => !a.IsDynamic)
            .SelectMany(static assembly =>
            {
                try
                {
                    return SensitiveDataDiscovery.Scan(assembly).Entries;
                }
                catch
                {
                    return [];
                }
            })
            .Any(static entry => entry.RetentionPolicy is not null);

        if (!hasRetentionAnnotations)
        {
            return;
        }

        var executorType = Type.GetType("SensitiveFlow.Retention.Services.RetentionExecutor, SensitiveFlow.Retention");
        var handlerType = Type.GetType("SensitiveFlow.Retention.Contracts.IRetentionExpirationHandler, SensitiveFlow.Retention");
        var hasExecutor = executorType is not null && provider.GetService(executorType) is not null;
        var hasHandlers = handlerType is not null && provider.GetServices(handlerType).Any();

        if (!hasExecutor && !hasHandlers)
        {
            diagnostics.Add(Warning("SF-CONFIG-010", "Retention annotations were found in loaded assemblies, but no RetentionExecutor or IRetentionExpirationHandler registration was found."));
        }
    }

    private static void AddAspNetCoreDiagnostics(
        IServiceProvider provider,
        ICollection<SensitiveFlowConfigurationDiagnostic> diagnostics)
    {
        var diagnosticsType = Type.GetType("SensitiveFlow.AspNetCore.Diagnostics.SensitiveFlowAspNetCorePipelineDiagnostics, SensitiveFlow.AspNetCore");
        var aspNetCoreDiagnostics = diagnosticsType is null ? null : provider.GetService(diagnosticsType);
        if (aspNetCoreDiagnostics is null)
        {
            return;
        }

        var middlewareRegistered = (bool)(diagnosticsType!.GetProperty("AuditMiddlewareRegistered")?.GetValue(aspNetCoreDiagnostics) ?? false);
        if (!middlewareRegistered)
        {
            diagnostics.Add(Warning("SF-CONFIG-011", "SensitiveFlow ASP.NET Core services are registered, but UseSensitiveFlowAudit() has not marked the pipeline."));
        }

        var observedAuthenticatedUser = (bool)(diagnosticsType.GetProperty("ObservedAuthenticatedUserBeforeAuditMiddleware")?.GetValue(aspNetCoreDiagnostics) ?? false);
        if (observedAuthenticatedUser)
        {
            diagnostics.Add(Warning("SF-CONFIG-012", "SensitiveFlow audit middleware observed an already-authenticated user before it ran; it may be registered after authentication."));
        }
    }

    private static void AddOutboxDiagnostics(
        IServiceProvider provider,
        ICollection<SensitiveFlowConfigurationDiagnostic> diagnostics)
    {
        var outbox = provider.GetService<IAuditOutbox>();
        var environment = provider.GetService<IHostEnvironment>();
        if (outbox?.GetType().FullName == "SensitiveFlow.Audit.Outbox.InMemoryAuditOutbox"
            && environment?.IsDevelopment() == false)
        {
            diagnostics.Add(Warning("SF-CONFIG-013", "In-memory audit outbox is configured outside Development. Use AddEfCoreAuditOutbox() or AddAuditOutbox<TOutbox>()."));
        }

        var durableOutbox = provider.GetService<IDurableAuditOutbox>();
        if (durableOutbox is not null && !provider.GetServices<IAuditOutboxPublisher>().Any())
        {
            diagnostics.Add(Warning("SF-CONFIG-014", "IDurableAuditOutbox is registered, but no IAuditOutboxPublisher registration was found."));
        }
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
