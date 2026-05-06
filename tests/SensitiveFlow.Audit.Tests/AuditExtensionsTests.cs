using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Tests.Stores;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class AuditExtensionsTests
{
    [Fact]
    public void RegisteredIAuditStore_ResolvesAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        var provider = services.BuildServiceProvider();

        var a = provider.GetRequiredService<IAuditStore>();
        var b = provider.GetRequiredService<IAuditStore>();

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void RegisteredIAuditStore_ResolvesCorrectType()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IAuditStore>().Should().BeOfType<InMemoryAuditStore>();
    }

    [Fact]
    public async Task RegisteredIAuditStore_AppendAndQuery_WorkEndToEnd()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();

        var record = new AuditRecord
        {
            DataSubjectId = "subject-1",
            Entity = "Customer",
            Field = "Email",
        };

        await store.AppendAsync(record);

        var results = await store.QueryAsync();
        results.Should().ContainSingle(r => r.Id == record.Id);
    }
}
