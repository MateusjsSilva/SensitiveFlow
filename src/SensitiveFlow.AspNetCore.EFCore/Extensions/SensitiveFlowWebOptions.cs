using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Anonymization.Decorators;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Diagnostics.Validation;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
using SensitiveFlow.Logging.Configuration;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.AspNetCore.EFCore.Extensions;

/// <summary>
/// Fluent builder for configuring the high-level SensitiveFlow web composition.
/// </summary>
public sealed class SensitiveFlowWebOptions
{
    /// <summary>
    /// The profile to apply. Defaults to <see cref="SensitiveFlowProfile.Balanced"/>.
    /// </summary>
    public SensitiveFlowProfile Profile { get; set; } = SensitiveFlowDefaults.Profile;

    /// <summary>
    /// Gets whether the EF Core audit store is enabled.
    /// </summary>
    public bool AuditStoreEnabled { get; private set; }

    /// <summary>
    /// Gets whether the EF Core token store is enabled.
    /// </summary>
    public bool TokenStoreEnabled { get; private set; }

    /// <summary>
    /// Gets whether the durable audit outbox is enabled.
    /// </summary>
    public bool OutboxEnabled { get; private set; }

    /// <summary>
    /// Gets whether JSON redaction is enabled.
    /// </summary>
    public bool JsonRedactionEnabled { get; private set; }

    /// <summary>
    /// Gets whether logging redaction is enabled.
    /// </summary>
    public bool LoggingRedactionEnabled { get; private set; }

    /// <summary>
    /// Gets whether EF Core audit interception is enabled.
    /// </summary>
    public bool EfCoreAuditEnabled { get; private set; }

    /// <summary>
    /// Gets whether ASP.NET Core context enrichment is enabled.
    /// </summary>
    public bool AspNetCoreContextEnabled { get; private set; }

    /// <summary>
    /// Gets whether startup validation is enabled.
    /// </summary>
    public bool ValidationEnabled { get; private set; }

    /// <summary>
    /// Gets whether health checks are enabled.
    /// </summary>
    public bool HealthChecksEnabled { get; private set; }

    /// <summary>
    /// Gets whether diagnostics (OpenTelemetry) wrapping is enabled.
    /// </summary>
    public bool DiagnosticsEnabled { get; private set; }

    /// <summary>
    /// Gets whether audit store retry is enabled.
    /// </summary>
    public bool AuditStoreRetryEnabled { get; private set; }

    /// <summary>
    /// Gets whether caching token store is enabled.
    /// </summary>
    public bool CachingTokenStoreEnabled { get; private set; }

    /// <summary>
    /// Gets whether data-subject export is enabled.
    /// </summary>
    public bool DataSubjectExportEnabled { get; private set; }

    /// <summary>
    /// Gets whether data-subject erasure is enabled.
    /// </summary>
    public bool DataSubjectErasureEnabled { get; private set; }

    /// <summary>
    /// Gets whether retention services are enabled.
    /// </summary>
    public bool RetentionEnabled { get; private set; }

    /// <summary>
    /// Gets whether the retention executor is enabled.
    /// </summary>
    public bool RetentionExecutorEnabled { get; private set; }

    /// <summary>
    /// Optional callback for custom policy overrides applied after the profile.
    /// </summary>
    internal Action<SensitiveFlowOptions>? PoliciesConfigure { get; private set; }

    /// <summary>
    /// The action used to configure the EF Core audit store.
    /// </summary>
    internal Action<DbContextOptionsBuilder>? AuditStoreOptionsAction { get; private set; }

    /// <summary>
    /// The action used to configure the EF Core token store.
    /// </summary>
    internal Action<DbContextOptionsBuilder>? TokenStoreOptionsAction { get; private set; }

    /// <summary>
    /// Optional configuration for the audit outbox dispatcher.
    /// </summary>
    internal Action<AuditOutboxDispatcherOptions>? OutboxConfigure { get; private set; }

    /// <summary>
    /// Optional configuration for JSON redaction.
    /// </summary>
    internal Action<JsonRedactionOptions>? JsonRedactionConfigure { get; private set; }

    /// <summary>
    /// Optional configuration for logging redaction.
    /// </summary>
    internal Action<SensitiveLoggingOptions>? LoggingConfigure { get; private set; }

    /// <summary>
    /// Optional configuration for startup validation.
    /// </summary>
    internal Action<SensitiveFlowValidationOptions>? ValidationConfigure { get; private set; }

    /// <summary>
    /// Optional configuration for audit store retry.
    /// </summary>
    internal Action<RetryingAuditStoreOptions>? RetryConfigure { get; private set; }

    /// <summary>
    /// Optional configuration for caching token store.
    /// </summary>
    internal Action<CachingTokenStoreOptions>? CachingTokenStoreConfigure { get; private set; }

    /// <summary>
    /// Optional configuration for retention executor.
    /// </summary>
    internal Action<RetentionExecutorOptions>? RetentionExecutorConfigure { get; private set; }

    /// <summary>
    /// Applies a built-in profile.
    /// </summary>
    public SensitiveFlowWebOptions UseProfile(SensitiveFlowProfile profile)
    {
        Profile = profile;
        return this;
    }

    /// <summary>
    /// Applies custom policy overrides after the profile is applied.
    /// Use this for fine-grained per-category rules (e.g. downgrading
    /// OmitInJson to RedactInJson for a specific category, or adding
    /// audit flags).
    /// </summary>
    public SensitiveFlowWebOptions ConfigurePolicies(Action<SensitiveFlowOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        PoliciesConfigure = configure;
        return this;
    }

    /// <summary>
    /// Enables the EF Core audit store with the given provider configuration.
    /// </summary>
    public SensitiveFlowWebOptions UseEfCoreAuditStore(Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        AuditStoreEnabled = true;
        AuditStoreOptionsAction = configure;
        return this;
    }

    /// <summary>
    /// Enables the EF Core audit store and token store in one provider-agnostic call.
    /// The callbacks can use any EF Core provider package referenced by the application
    /// (SQL Server, PostgreSQL, SQLite, MySQL, or another provider).
    /// </summary>
    public SensitiveFlowWebOptions UseEfCoreStores(
        Action<DbContextOptionsBuilder> configureAuditStore,
        Action<DbContextOptionsBuilder> configureTokenStore)
    {
        UseEfCoreAuditStore(configureAuditStore);
        UseEfCoreTokenStore(configureTokenStore);
        return this;
    }

    /// <summary>
    /// Enables the EF Core token store with the given provider configuration.
    /// </summary>
    public SensitiveFlowWebOptions UseEfCoreTokenStore(Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        TokenStoreEnabled = true;
        TokenStoreOptionsAction = configure;
        return this;
    }

    /// <summary>
    /// Enables the durable audit outbox with optional dispatcher configuration.
    /// </summary>
    public SensitiveFlowWebOptions EnableOutbox(Action<AuditOutboxDispatcherOptions>? configure = null)
    {
        OutboxEnabled = true;
        OutboxConfigure = configure;
        return this;
    }

    /// <summary>
    /// Enables JSON redaction with optional configuration.
    /// </summary>
    public SensitiveFlowWebOptions EnableJsonRedaction(Action<JsonRedactionOptions>? configure = null)
    {
        JsonRedactionEnabled = true;
        JsonRedactionConfigure = configure;
        return this;
    }

    /// <summary>
    /// Enables logging redaction with optional configuration.
    /// </summary>
    public SensitiveFlowWebOptions EnableLoggingRedaction(Action<SensitiveLoggingOptions>? configure = null)
    {
        LoggingRedactionEnabled = true;
        LoggingConfigure = configure;
        return this;
    }

    /// <summary>
    /// Enables EF Core audit interception.
    /// </summary>
    public SensitiveFlowWebOptions EnableEfCoreAudit()
    {
        EfCoreAuditEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables ASP.NET Core context enrichment (actor, IP pseudonymization).
    /// </summary>
    public SensitiveFlowWebOptions EnableAspNetCoreContext()
    {
        AspNetCoreContextEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables startup validation with optional configuration.
    /// </summary>
    public SensitiveFlowWebOptions EnableValidation(Action<SensitiveFlowValidationOptions>? configure = null)
    {
        ValidationEnabled = true;
        ValidationConfigure = configure;
        return this;
    }

    /// <summary>
    /// Enables health checks for registered SensitiveFlow infrastructure.
    /// </summary>
    public SensitiveFlowWebOptions EnableHealthChecks()
    {
        HealthChecksEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables OpenTelemetry diagnostics wrapping on the audit store.
    /// </summary>
    public SensitiveFlowWebOptions EnableDiagnostics()
    {
        DiagnosticsEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables retry decorator on the audit store with optional configuration.
    /// </summary>
    public SensitiveFlowWebOptions EnableAuditStoreRetry(Action<RetryingAuditStoreOptions>? configure = null)
    {
        AuditStoreRetryEnabled = true;
        RetryConfigure = configure;
        return this;
    }

    /// <summary>
    /// Enables caching token store with optional configuration.
    /// </summary>
    public SensitiveFlowWebOptions EnableCachingTokenStore(Action<CachingTokenStoreOptions>? configure = null)
    {
        CachingTokenStoreEnabled = true;
        CachingTokenStoreConfigure = configure;
        return this;
    }

    /// <summary>
    /// Enables data-subject export service.
    /// </summary>
    public SensitiveFlowWebOptions EnableDataSubjectExport()
    {
        DataSubjectExportEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables data-subject erasure service.
    /// </summary>
    public SensitiveFlowWebOptions EnableDataSubjectErasure()
    {
        DataSubjectErasureEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables retention evaluation services.
    /// </summary>
    public SensitiveFlowWebOptions EnableRetention()
    {
        RetentionEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables the retention executor with optional configuration.
    /// </summary>
    public SensitiveFlowWebOptions EnableRetentionExecutor(Action<RetentionExecutorOptions>? configure = null)
    {
        RetentionExecutorEnabled = true;
        RetentionExecutorConfigure = configure;
        return this;
    }

}
