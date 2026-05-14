using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SensitiveFlow.AspNetCore.Diagnostics;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Diagnostics.Extensions;
using SensitiveFlow.Diagnostics.Validation;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.Json.Configuration;

namespace SensitiveFlow.Diagnostics.Tests;

public sealed class SensitiveFlowValidationTests
{
    [Fact]
    public void ValidateSensitiveFlow_WhenAuditStoreRequiredAndMissing_ReportsError()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowValidation(o =>
        {
            o.RequireAuditStore = true;
            o.FailOnError = false;
        });

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d =>
            d.Code == "SF-CONFIG-001" && d.Severity == SensitiveFlowDiagnosticSeverity.Error);
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenRequiredStoresExist_ReturnsNoStoreWarnings()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, HealthyAuditStore>();
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = true);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().NotContain(d => d.Code == "SF-CONFIG-001");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenTokenStoreRequiredAndMissing_ReportsError()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowValidation(o =>
        {
            o.RequireTokenStore = true;
            o.FailOnError = false;
        });

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d =>
            d.Code == "SF-CONFIG-002" && d.Severity == SensitiveFlowDiagnosticSeverity.Error);
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenJsonRedactionRequiredAndOptionsMissing_ReportsWarning()
    {
        _ = typeof(JsonRedactionOptions).Assembly;
        var services = new ServiceCollection();
        services.AddSensitiveFlowValidation(o =>
        {
            o.RequireAuditStore = false;
            o.RequireJsonRedaction = true;
        });

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-004");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenRetentionRequiredAndServicesMissing_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowValidation(o =>
        {
            o.RequireAuditStore = false;
            o.RequireRetention = true;
        });

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-005");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenPseudonymizerWithoutTokenStore_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPseudonymizer, FakePseudonymizer>();
        services.AddSensitiveFlowValidation();

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-003");
    }

    [Fact]
    public void ValidateSensitiveFlow_WithoutRegisteredValidator_UsesDefaultValidator()
    {
        // The default validator requires an audit store and (now) fails on error,
        // which is the documented behavior. We assert that contract here.
        var services = new ServiceCollection().BuildServiceProvider();

        Action act = () => services.ValidateSensitiveFlow();

        act.Should().Throw<SensitiveFlow.Core.Exceptions.SensitiveFlowConfigurationException>()
            .Which.Message.Should().Contain("SF-CONFIG-001");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenPolicyRequiresAuditAndStoreMissing_ReportsWarning()
    {
        var services = new ServiceCollection();
        var options = new SensitiveFlowOptions();
        options.Policies.ForSensitiveCategory(SensitiveDataCategory.Other).RequireAudit();
        services.AddSingleton(options);
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-006");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenPolicyRequiresJsonAndJsonOptionsMissing_ReportsWarning()
    {
        var services = new ServiceCollection();
        var options = new SensitiveFlowOptions();
        options.Policies.ForCategory(DataCategory.Contact).RedactInJson();
        services.AddSingleton(options);
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-007");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenPolicyRequiresLogMaskingAndRedactorMissing_ReportsWarning()
    {
        var services = new ServiceCollection();
        var options = new SensitiveFlowOptions();
        options.Policies.ForCategory(DataCategory.Contact).MaskInLogs();
        services.AddSingleton(options);
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-008");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenEfCoreInterceptorRegisteredWithoutAuditStore_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowEFCore();
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-009");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenRetentionAnnotationsLoadedWithoutExecutorOrHandlers_ReportsWarning()
    {
        _ = typeof(RetentionAnnotatedShape).Assembly;
        var services = new ServiceCollection();
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-010");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenAspNetCoreRegisteredButMiddlewareNotMarked_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowAspNetCore();
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-011");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenAuditMiddlewareObservedAuthenticatedUser_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowAspNetCore();
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);
        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<SensitiveFlowAspNetCorePipelineDiagnostics>();
        pipeline.MarkAuditMiddlewareRegistered();
        pipeline.MarkAuthenticatedUserObserved();

        var report = provider.ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-012");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenInMemoryOutboxRegisteredOutsideDevelopment_ReportsWarning()
    {
        var services = new ServiceCollection();
#pragma warning disable CS0618
        services.AddSingleton<IAuditOutbox, InMemoryAuditOutbox>();
#pragma warning restore CS0618
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Production"));
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-013");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenInMemoryOutboxRegisteredInDevelopment_DoesNotReportProductionWarning()
    {
        var services = new ServiceCollection();
#pragma warning disable CS0618
        services.AddSingleton<IAuditOutbox, InMemoryAuditOutbox>();
#pragma warning restore CS0618
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Development));
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().NotContain(d => d.Code == "SF-CONFIG-013");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenDurableOutboxHasNoPublisher_ReportsError()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDurableAuditOutbox, FakeDurableOutbox>();
        services.AddSensitiveFlowValidation(o =>
        {
            o.RequireAuditStore = false;
            o.FailOnError = false;
        });

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d =>
            d.Code == "SF-CONFIG-014" && d.Severity == SensitiveFlowDiagnosticSeverity.Error);
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenFailOnErrorEnabled_ThrowsOnErrorDiagnostic()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDurableAuditOutbox, FakeDurableOutbox>();
        services.AddSensitiveFlowValidation(o =>
        {
            o.RequireAuditStore = false;
            o.FailOnError = true;
        });

        Action act = () => services.BuildServiceProvider().ValidateSensitiveFlow();

        act.Should().Throw<SensitiveFlow.Core.Exceptions.SensitiveFlowConfigurationException>()
            .Which.Code.Should().Be("SF-CONFIG-FAIL");
    }

    [Fact]
    public void ValidateSensitiveFlow_WhenDurableOutboxHasPublisher_DoesNotReportPublisherWarning()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDurableAuditOutbox, FakeDurableOutbox>();
        services.AddSingleton<IAuditOutboxPublisher, FakePublisher>();
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = false);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().NotContain(d => d.Code == "SF-CONFIG-014");
    }

    [Fact]
    public void AddSensitiveFlow_RegistersSharedOptions()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlow(options => options.UseProfile(SensitiveFlowProfile.Strict));

        var options = services.BuildServiceProvider().GetRequiredService<SensitiveFlowOptions>();

        options.Profile.Should().Be(SensitiveFlowProfile.Strict);
    }

    private sealed class HealthyAuditStore : IAuditStore
    {
        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AuditRecord>>([]);
        }

        public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(string dataSubjectId, DateTimeOffset? from = null, DateTimeOffset? to = null, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AuditRecord>>([]);
        }

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AuditRecord>>([]);
        }
    }

    private sealed class FakePseudonymizer : IPseudonymizer
    {
        public string Pseudonymize(string value) => "token";

        public Task<string> PseudonymizeAsync(string value, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("token");
        }

        public string Reverse(string token) => "value";

        public Task<string> ReverseAsync(string token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("value");
        }

        public bool CanPseudonymize(string value) => true;
    }

    private sealed class RetentionAnnotatedShape
    {
        [RetentionData(Years = 1)]
        public string Email { get; set; } = string.Empty;
    }

    private sealed class FakeDurableOutbox : IDurableAuditOutbox
    {
        public Task EnqueueAsync(AuditRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<AuditOutboxEntry>> DequeueBatchAsync(int max, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditOutboxEntry>>([]);

        public Task MarkProcessedAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MarkDeadLetteredAsync(Guid id, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePublisher : IAuditOutboxPublisher
    {
        public Task PublishAsync(AuditOutboxEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "SensitiveFlow.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
