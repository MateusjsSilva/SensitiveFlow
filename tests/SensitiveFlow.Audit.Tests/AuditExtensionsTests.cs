using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.Audit.Stores;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Audit.Tests;

public sealed class AuditExtensionsTests
{
    [Fact]
    public void AddInMemoryAuditStore_RegistersIAuditStore()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAuditStore();

        var provider = services.BuildServiceProvider();

        provider.GetService<IAuditStore>().Should().BeOfType<InMemoryAuditStore>();
    }

    [Fact]
    public void AddInMemoryAuditStore_RegistersAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAuditStore();

        var provider = services.BuildServiceProvider();

        var a = provider.GetRequiredService<IAuditStore>();
        var b = provider.GetRequiredService<IAuditStore>();

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void AddInMemoryAuditStore_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddInMemoryAuditStore();

        result.Should().BeSameAs(services);
    }
}
