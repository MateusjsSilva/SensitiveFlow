using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SensitiveFlow.AspNetCore.EFCore.Extensions;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Anonymization.Decorators;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Diagnostics.Validation;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;
using SensitiveFlow.Logging.Configuration;
using SensitiveFlow.Logging.Loggers;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.AspNetCore.EFCore.Tests;

public sealed class SensitiveFlowWebServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSensitiveFlowWeb_RejectsNullArguments()
    {
        var services = new ServiceCollection();

        var nullServices = () => SensitiveFlowWebServiceCollectionExtensions.AddSensitiveFlowWeb(null!, _ => { });
        var nullConfigure = () => services.AddSensitiveFlowWeb(null!);

        nullServices.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
        nullConfigure.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }

    [Fact]
    public void SensitiveFlowWebOptions_FluentMethods_EnableExpectedFlags()
    {
        var options = new SensitiveFlowWebOptions();
        Action<DbContextOptionsBuilder> audit = builder => builder.UseSqlite("Data Source=:memory:");
        Action<DbContextOptionsBuilder> token = builder => builder.UseSqlite("Data Source=:memory:");
        Action<AuditOutboxDispatcherOptions> outbox = o => o.BatchSize = 2;
        Action<JsonRedactionOptions> json = o => o.DefaultMode = JsonRedactionMode.Omit;
        Action<SensitiveLoggingOptions> logging = o => o.RedactedPlaceholder = "***";
        Action<SensitiveFlowValidationOptions> validation = o => o.RequireAuditStore = true;
        Action<RetryingAuditStoreOptions> retry = o => o.MaxAttempts = 2;
        Action<CachingTokenStoreOptions> cache = o => o.MaxEntries = 10;
        Action<RetentionExecutorOptions> retention = o => o.AnonymousStringMarker = "[gone]";

        var returned = options
            .UseProfile(SensitiveFlowProfile.Strict)
            .ConfigurePolicies(o => o.Policies.ForCategory(DataCategory.Contact).MaskInLogs())
            .UseEfCoreStores(audit, token)
            .EnableOutbox(outbox)
            .EnableJsonRedaction(json)
            .EnableLoggingRedaction(logging)
            .EnableEfCoreAudit()
            .EnableAspNetCoreContext()
            .EnableValidation(validation)
            .EnableHealthChecks()
            .EnableDiagnostics()
            .EnableAuditStoreRetry(retry)
            .EnableCachingTokenStore(cache)
            .EnableDataSubjectExport()
            .EnableDataSubjectErasure()
            .EnableRetention()
            .EnableRetentionExecutor(retention);

        returned.Should().BeSameAs(options);
        options.Profile.Should().Be(SensitiveFlowProfile.Strict);
        options.AuditStoreEnabled.Should().BeTrue();
        options.TokenStoreEnabled.Should().BeTrue();
        options.OutboxEnabled.Should().BeTrue();
        options.JsonRedactionEnabled.Should().BeTrue();
        options.LoggingRedactionEnabled.Should().BeTrue();
        options.EfCoreAuditEnabled.Should().BeTrue();
        options.AspNetCoreContextEnabled.Should().BeTrue();
        options.ValidationEnabled.Should().BeTrue();
        options.HealthChecksEnabled.Should().BeTrue();
        options.DiagnosticsEnabled.Should().BeTrue();
        options.AuditStoreRetryEnabled.Should().BeTrue();
        options.CachingTokenStoreEnabled.Should().BeTrue();
        options.DataSubjectExportEnabled.Should().BeTrue();
        options.DataSubjectErasureEnabled.Should().BeTrue();
        options.RetentionEnabled.Should().BeTrue();
        options.RetentionExecutorEnabled.Should().BeTrue();
    }

    [Fact]
    public void SensitiveFlowWebOptions_RejectsNullCallbacks()
    {
        var options = new SensitiveFlowWebOptions();

        options.Invoking(o => o.ConfigurePolicies(null!)).Should().Throw<ArgumentNullException>();
        options.Invoking(o => o.UseEfCoreAuditStore(null!)).Should().Throw<ArgumentNullException>();
        options.Invoking(o => o.UseEfCoreTokenStore(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSensitiveFlowWeb_DoesNotRegisterDatabaseInitializer()
    {
        var services = new ServiceCollection();

        services.AddSensitiveFlowWeb(options =>
        {
            options.UseEfCoreStores(
                audit => audit.UseSqlite("Data Source=:memory:"),
                tokens => tokens.UseSqlite("Data Source=:memory:"));
        });

        services.Should().NotContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddSensitiveFlowWeb_RegistersProviderAgnosticStoresAndInterceptor()
    {
        var services = new ServiceCollection();

        services.AddSensitiveFlowWeb(options =>
        {
            options.UseEfCoreStores(
                audit => audit.UseSqlite("Data Source=:memory:"),
                tokens => tokens.UseSqlite("Data Source=:memory:"));
            options.EnableEfCoreAudit();
        });

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuditStore>().Should().NotBeNull();
        provider.GetRequiredService<ITokenStore>().Should().NotBeNull();
        provider.GetRequiredService<SensitiveDataAuditInterceptor>().Should().NotBeNull();
    }

    [Fact]
    public void AddSensitiveFlowWeb_EnableJsonRedaction_ConfiguresHttpJsonOptions()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowWeb(options => options.EnableJsonRedaction());
        using var provider = services.BuildServiceProvider();
        var jsonOptions = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value
            .SerializerOptions;

        var json = JsonSerializer.Serialize(new Customer { Email = "alice@example.test" }, jsonOptions);

        json.Should().NotContain("alice@example.test");
        json.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void AddSensitiveFlowWeb_EnableJsonRedaction_ConfiguresMvcJsonOptions()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowWeb(options => options.EnableJsonRedaction());
        using var provider = services.BuildServiceProvider();
        var jsonOptions = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
            .Value
            .JsonSerializerOptions;

        var json = JsonSerializer.Serialize(new Customer { Email = "alice@example.test" }, jsonOptions);

        json.Should().NotContain("alice@example.test");
        json.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void AddSensitiveFlowWeb_EnableLoggingRedaction_WrapsExistingLoggerProviders()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerProvider, CapturingLoggerProvider>();

        services.AddSensitiveFlowWeb(options => options.EnableLoggingRedaction());
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ILoggerProvider>()
            .Should()
            .ContainSingle(provider => provider is RedactingLoggerProvider);
    }

    [Fact]
    public void AddSensitiveFlowWeb_EnableOutbox_WrapsAuditStore()
    {
        var services = new ServiceCollection();

        services.AddSensitiveFlowWeb(options =>
        {
            options.UseEfCoreStores(
                audit => audit.UseSqlite("Data Source=:memory:"),
                tokens => tokens.UseSqlite("Data Source=:memory:"));
            options.EnableOutbox();
        });

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuditStore>().Should().BeOfType<OutboxAuditStore>();
    }

    [Fact]
    public void AddSensitiveFlowWeb_FullComposition_RegistersOptionalServices()
    {
        var services = new ServiceCollection();

        services.AddSensitiveFlowWeb(options =>
        {
            options.UseEfCoreStores(
                audit => audit.UseSqlite("Data Source=:memory:"),
                tokens => tokens.UseSqlite("Data Source=:memory:"));
            options.ConfigurePolicies(o => o.Policies.ForCategory(DataCategory.Contact).MaskInLogs());
            options.EnableAuditStoreRetry();
            options.EnableDiagnostics();
            options.EnableOutbox();
            options.EnableCachingTokenStore();
            options.EnableDataSubjectExport();
            options.EnableDataSubjectErasure();
            options.EnableLoggingRedaction();
            options.EnableEfCoreAudit();
            options.EnableAspNetCoreContext();
            options.EnableJsonRedaction(o => o.NonStringRedactionMode = JsonNonStringRedactionMode.Omit);
            options.EnableRetention();
            options.EnableRetentionExecutor(o => o.AnonymousStringMarker = "[gone]");
            options.EnableValidation();
            options.EnableHealthChecks();
        });

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<SensitiveFlowOptions>().Policies.Find(DataCategory.Contact).Should().NotBeNull();
        provider.GetRequiredService<ITokenStore>().Should().NotBeNull();
        provider.GetRequiredService<IAuditStore>().Should().NotBeNull();
        provider.GetRequiredService<SensitiveDataAuditInterceptor>().Should().NotBeNull();
        provider.GetRequiredService<IOptions<JsonRedactionOptions>>()
            .Value.NonStringRedactionMode.Should().Be(JsonNonStringRedactionMode.Omit);
    }

    private sealed class Customer
    {
        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

        public void Dispose()
        {
        }
    }

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
