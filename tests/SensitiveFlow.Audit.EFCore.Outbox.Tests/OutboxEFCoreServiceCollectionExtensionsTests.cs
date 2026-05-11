using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SensitiveFlow.Audit.EFCore;
using SensitiveFlow.Audit.EFCore.Outbox.Extensions;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Audit.EFCore.Outbox.Tests;

public sealed class OutboxEFCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEfCoreAuditOutbox_RegistersDurableOutbox()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AuditDbContext>(options => options.UseSqlite("Data Source=:memory:"));

        services.AddEfCoreAuditOutbox();
        var provider = services.BuildServiceProvider();

        provider.GetService<IDurableAuditOutbox>().Should().NotBeNull();
    }

    [Fact]
    public void AddEfCoreAuditOutbox_RegistersIAuditOutbox()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AuditDbContext>(options => options.UseSqlite("Data Source=:memory:"));

        services.AddEfCoreAuditOutbox();
        var provider = services.BuildServiceProvider();

        provider.GetService<IAuditOutbox>().Should().NotBeNull();
    }

    [Fact]
    public void AddEfCoreAuditOutbox_RegistersDispatcherOptions()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AuditDbContext>(options => options.UseSqlite("Data Source=:memory:"));

        services.AddEfCoreAuditOutbox(options => options.BatchSize = 42);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<AuditOutboxDispatcherOptions>();
        options.BatchSize.Should().Be(42);
    }

    [Fact]
    public void AddEfCoreAuditOutbox_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AuditDbContext>(options => options.UseSqlite("Data Source=:memory:"));

        services.AddEfCoreAuditOutbox();

        var hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        hostedServices.Should().ContainSingle(d => d.ImplementationType == typeof(AuditOutboxDispatcher));
    }

    [Fact]
    public void AddEfCoreAuditOutbox_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddEfCoreAuditOutbox();

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }
}
