using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.AspNetCore.Diagnostics;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Diagnostics.Extensions;
using SensitiveFlow.Diagnostics.Validation;
using SensitiveFlow.EFCore.Extensions;

namespace SensitiveFlow.Diagnostics.Tests;

public sealed class SensitiveFlowValidationTests
{
    [Fact]
    public void ValidateSensitiveFlow_WhenAuditStoreRequiredAndMissing_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowValidation(o => o.RequireAuditStore = true);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-001");
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
    public void ValidateSensitiveFlow_WhenTokenStoreRequiredAndMissing_ReportsWarning()
    {
        var services = new ServiceCollection();
        services.AddSensitiveFlowValidation(o => o.RequireTokenStore = true);

        var report = services.BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-002");
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
        var report = new ServiceCollection().BuildServiceProvider().ValidateSensitiveFlow();

        report.Diagnostics.Should().Contain(d => d.Code == "SF-CONFIG-001");
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
}
