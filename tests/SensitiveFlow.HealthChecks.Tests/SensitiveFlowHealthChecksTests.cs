using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.HealthChecks.Checks;
using SensitiveFlow.HealthChecks.Extensions;

namespace SensitiveFlow.HealthChecks.Tests;

public sealed class SensitiveFlowHealthChecksTests
{
    [Fact]
    public void AddSensitiveFlowHealthChecks_RegistersChecks()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, HealthyAuditStore>();
        services.AddSingleton<ITokenStore, HealthyTokenStore>();
        services.AddLogging();

        services.AddSensitiveFlowHealthChecks()
            .AddAuditStoreCheck()
            .AddTokenStoreCheck();

        var provider = services.BuildServiceProvider();
        var checks = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>().Value.Registrations;

        checks.Should().Contain(r => r.Name == "sensitiveflow-audit-store");
        checks.Should().Contain(r => r.Name == "sensitiveflow-token-store");
    }

    [Fact]
    public async Task AddSensitiveFlowHealthChecks_RegisteredChecksRunHealthy()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, HealthyAuditStore>();
        services.AddSingleton<ITokenStore, HealthyTokenStore>();
        services.AddLogging();
        services.AddSensitiveFlowHealthChecks()
            .AddAuditStoreCheck()
            .AddTokenStoreCheck();

        var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task AuditStoreCheck_WhenProbeFails_ReturnsUnhealthy()
    {
        var check = new AuditStoreHealthCheck(new FailingAuditStore());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task TokenStoreCheck_WhenProbeFails_ReturnsUnhealthy()
    {
        var check = new TokenStoreHealthCheck(new FailingTokenStore());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
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

    private sealed class HealthyTokenStore : ITokenStore
    {
        public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("token");
        }

        public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("value");
        }
    }

    private sealed class FailingAuditStore : IAuditStore
    {
        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("append failed");
        }

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("query failed");
        }

        public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(string dataSubjectId, DateTimeOffset? from = null, DateTimeOffset? to = null, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("query failed");
        }
    }

    private sealed class FailingTokenStore : ITokenStore, IHealthProbe
    {
        public Task ProbeAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("probe failed");
        }

        public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("token failed");
        }

        public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("token failed");
        }
    }
}
