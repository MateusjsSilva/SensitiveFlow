using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.EFCore.Outbox.Extensions;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Anonymization.Decorators;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Diagnostics.Extensions;
using SensitiveFlow.Diagnostics.Validation;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.HealthChecks.Extensions;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
using SensitiveFlow.Json.Extensions;
using SensitiveFlow.Logging.Configuration;
using SensitiveFlow.Logging.Extensions;
using SensitiveFlow.Logging.Loggers;
using SensitiveFlow.Logging.Redaction;
using SensitiveFlow.Retention.Extensions;
using SensitiveFlow.Retention.Services;
using SensitiveFlow.TokenStore.EFCore;
using SensitiveFlow.TokenStore.EFCore.Extensions;

namespace SensitiveFlow.AspNetCore.EFCore.Extensions;

/// <summary>
/// High-level DI extensions that compose the recommended ASP.NET Core + EF Core
/// SensitiveFlow stack with a single call.
/// </summary>
/// <remarks>
/// The granular extension methods (e.g. <c>AddEfCoreAuditStore</c>,
/// <c>AddSensitiveFlowEFCore</c>) continue to exist for advanced scenarios.
/// This composition layer is the official "happy path" for new adopters.
/// </remarks>
public static class SensitiveFlowWebServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full recommended SensitiveFlow web stack.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration callback for the fluent builder.</param>
    /// <example>
    /// <code>
    /// builder.Services.AddSensitiveFlowWeb(options =>
    /// {
    ///     options.UseProfile(SensitiveFlowProfile.Balanced);
    ///     options.UseEfCoreStores(
    ///         audit => audit.UseSqlite(builder.Configuration.GetConnectionString("Audit")),
    ///         tokens => tokens.UseSqlite(builder.Configuration.GetConnectionString("Tokens")));
    ///     options.EnableJsonRedaction();
    ///     options.EnableLoggingRedaction();
    ///     options.EnableEfCoreAudit();
    ///     options.EnableAspNetCoreContext();
    ///     options.EnableValidation();
    ///     options.EnableHealthChecks();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSensitiveFlowWeb(
        this IServiceCollection services,
        Action<SensitiveFlowWebOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SensitiveFlowWebOptions();
        configure(options);

        // 1. Core options (profile + policies)
        var sensitiveFlowOptions = new SensitiveFlowOptions();
        sensitiveFlowOptions.UseProfile(options.Profile);
        options.PoliciesConfigure?.Invoke(sensitiveFlowOptions);
        services.AddSingleton(sensitiveFlowOptions);

        // 2. Audit store
        if (options.AuditStoreEnabled)
        {
            if (options.AuditStoreOptionsAction is null)
            {
                throw new InvalidOperationException("Audit store is enabled but no DbContext configuration was provided. Call UseEfCoreAuditStore(...).");
            }

            services.AddEfCoreAuditStore(options.AuditStoreOptionsAction);
        }

        // 3. Audit store retry
        if (options.AuditStoreRetryEnabled)
        {
            services.AddAuditStoreRetry(options.RetryConfigure);
        }

        // 4. Diagnostics (must be after audit store registration)
        if (options.DiagnosticsEnabled)
        {
            services.AddSensitiveFlowDiagnostics();
        }

        // 5. Outbox (must be after audit store)
        if (options.OutboxEnabled)
        {
            services.AddEfCoreAuditOutbox(options.OutboxConfigure);
        }

        // 6. Token store
        if (options.TokenStoreEnabled)
        {
            if (options.TokenStoreOptionsAction is null)
            {
                throw new InvalidOperationException("Token store is enabled but no DbContext configuration was provided. Call UseEfCoreTokenStore(...).");
            }

            services.AddEfCoreTokenStore(options.TokenStoreOptionsAction);
        }

        // 7. Caching token store
        if (options.CachingTokenStoreEnabled)
        {
            services.AddCachingTokenStore(options.CachingTokenStoreConfigure);
        }

        // 8. Data-subject export / erasure
        if (options.DataSubjectExportEnabled)
        {
            services.AddDataSubjectExport();
        }

        if (options.DataSubjectErasureEnabled)
        {
            services.AddDataSubjectErasure();
        }

        // 9. Logging redaction
        if (options.LoggingRedactionEnabled)
        {
            AddLoggingRedaction(services, options.LoggingConfigure, sensitiveFlowOptions);
        }

        // 10. EF Core audit interceptor
        if (options.EfCoreAuditEnabled)
        {
            services.AddSensitiveFlowEFCore();
        }

        // 11. ASP.NET Core context enrichment
        if (options.AspNetCoreContextEnabled)
        {
            services.AddSensitiveFlowAspNetCore();
        }

        // 12. JSON redaction
        if (options.JsonRedactionEnabled)
        {
            JsonRedactionOptions jsonRedactionOptions;
            if (options.JsonRedactionConfigure is not null)
            {
                services.AddSensitiveFlowJsonRedaction(options.JsonRedactionConfigure);
                jsonRedactionOptions = new JsonRedactionOptions();
                options.JsonRedactionConfigure(jsonRedactionOptions);
            }
            else
            {
                jsonRedactionOptions = new JsonRedactionOptions
                {
                    DefaultMode = JsonRedactionMode.Mask,
                    Policies = sensitiveFlowOptions.Policies,
                };
                services.AddSensitiveFlowJsonRedaction(json =>
                {
                    json.DefaultMode = jsonRedactionOptions.DefaultMode;
                    json.Policies = jsonRedactionOptions.Policies;
                });
            }

            ConfigureAspNetCoreJsonRedaction(services, jsonRedactionOptions);
        }

        // 13. Retention
        if (options.RetentionEnabled)
        {
            services.AddRetention();
        }

        if (options.RetentionExecutorEnabled)
        {
            services.AddRetentionExecutor(options.RetentionExecutorConfigure);
        }

        // 14. Validation
        if (options.ValidationEnabled)
        {
            if (options.ValidationConfigure is not null)
            {
                services.AddSensitiveFlowValidation(options.ValidationConfigure);
            }
            else
            {
                services.AddSensitiveFlowValidation(validation =>
                {
                    validation.RequireAuditStore = options.AuditStoreEnabled;
                    validation.RequireTokenStore = options.TokenStoreEnabled;
                    validation.RequireJsonRedaction = options.JsonRedactionEnabled;
                    validation.RequireRetention = options.RetentionEnabled;
                });
            }
        }

        // 15. Health checks
        if (options.HealthChecksEnabled)
        {
            var healthBuilder = services.AddSensitiveFlowHealthChecks();

            if (options.AuditStoreEnabled)
            {
                healthBuilder.AddAuditStoreCheck();
            }

            if (options.TokenStoreEnabled)
            {
                healthBuilder.AddTokenStoreCheck();
            }

            if (options.OutboxEnabled)
            {
                healthBuilder.AddAuditOutboxCheck();
            }
        }

        return services;
    }

    /// <summary>
    /// Adds the SensitiveFlow audit middleware to the ASP.NET Core pipeline.
    /// Place this before authentication/authorization so the pseudonymized IP token
    /// is available throughout the request.
    /// </summary>
    public static IApplicationBuilder UseSensitiveFlow(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseSensitiveFlowAudit();
        return app;
    }

    private static void ConfigureAspNetCoreJsonRedaction(
        IServiceCollection services,
        JsonRedactionOptions redactionOptions)
    {
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.WithSensitiveDataRedaction(redactionOptions));

        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            options.JsonSerializerOptions.WithSensitiveDataRedaction(redactionOptions));
    }

    private static void AddLoggingRedaction(
        IServiceCollection services,
        Action<SensitiveLoggingOptions>? configure,
        SensitiveFlowOptions sensitiveFlowOptions)
    {
        if (configure is not null)
        {
            services.AddSensitiveFlowLogging(configure);
        }
        else
        {
            services.AddSensitiveFlowLogging(logging =>
            {
                logging.Policies = sensitiveFlowOptions.Policies;
            });
        }

        WrapRegisteredLoggerProviders(services);
    }

    private static void WrapRegisteredLoggerProviders(IServiceCollection services)
    {
        var providerDescriptors = services
            .Where(d => d.ServiceType == typeof(ILoggerProvider)
                        && d.ImplementationType != typeof(RedactingLoggerProvider))
            .ToList();

        foreach (var descriptor in providerDescriptors)
        {
            services.Remove(descriptor);
            services.Add(new ServiceDescriptor(
                typeof(ILoggerProvider),
                sp => new RedactingLoggerProvider(
                    CreateLoggerProvider(sp, descriptor),
                    sp.GetRequiredService<ISensitiveValueRedactor>(),
                    sp.GetRequiredService<SensitiveLoggingOptions>()),
                descriptor.Lifetime));
        }
    }

    private static ILoggerProvider CreateLoggerProvider(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is ILoggerProvider instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (ILoggerProvider)descriptor.ImplementationFactory(sp)!;
        }

        if (descriptor.ImplementationType is not null)
        {
            return (ILoggerProvider)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

        throw new InvalidOperationException("Unsupported ILoggerProvider registration.");
    }
}
