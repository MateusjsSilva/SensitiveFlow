using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.HealthChecks.Checks;
using SensitiveFlow.HealthChecks.Extensions;

namespace SensitiveFlow.HealthChecks.Tests;

#pragma warning disable CS0618 // Type or member is obsolete
public sealed class AuditOutboxHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WithDurableOutbox_ReturnsHealthy()
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var check = new AuditOutboxHealthCheck(outbox);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Audit outbox resolved");
    }

    [Fact]
    public async Task CheckHealthAsync_WithInMemoryOutbox_InDevelopment_ReturnsHealthy()
    {
        var outbox = new InMemoryAuditOutbox();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        var check = new AuditOutboxHealthCheck(outbox, env);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithInMemoryOutbox_InProduction_ReturnsDegraded()
    {
        var outbox = new InMemoryAuditOutbox();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        var check = new AuditOutboxHealthCheck(outbox, env);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("In-memory audit outbox");
    }

    [Fact]
    public async Task CheckHealthAsync_WithInMemoryOutbox_NoEnvironment_ReturnsHealthy()
    {
        var outbox = new InMemoryAuditOutbox();

        var check = new AuditOutboxHealthCheck(outbox, null);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void AddAuditOutboxCheck_RegistersCheck()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditOutbox>(new InMemoryAuditOutbox());
        services.AddLogging();

        services.AddHealthChecks()
            .AddAuditOutboxCheck();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;

        options.Registrations.Should().Contain(r => r.Name == "sensitiveflow-audit-outbox");
    }
}
#pragma warning restore CS0618 // Type or member is obsolete
