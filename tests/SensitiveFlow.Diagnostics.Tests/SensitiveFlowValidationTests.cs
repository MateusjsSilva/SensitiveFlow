using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Diagnostics.Extensions;
using SensitiveFlow.Diagnostics.Validation;

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
}
