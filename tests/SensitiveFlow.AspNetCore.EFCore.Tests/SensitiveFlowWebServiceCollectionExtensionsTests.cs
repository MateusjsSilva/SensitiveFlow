using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SensitiveFlow.AspNetCore.EFCore.Extensions;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.Logging.Loggers;

namespace SensitiveFlow.AspNetCore.EFCore.Tests;

public sealed class SensitiveFlowWebServiceCollectionExtensionsTests
{
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
