using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class AuditStoreExtensionsTests
{
    [Fact]
    public void AddAuditStore_RegistersIAuditStore()
    {
        var services = new ServiceCollection();
        services.AddAuditStore<FakeAuditStore>();

        var provider = services.BuildServiceProvider();

        provider.GetService<IAuditStore>().Should().BeOfType<FakeAuditStore>();
    }

    [Fact]
    public void AddAuditStore_RegistersAsScoped()
    {
        var services = new ServiceCollection();
        services.AddAuditStore<FakeAuditStore>();

        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var a = scope1.ServiceProvider.GetRequiredService<IAuditStore>();
        var b = scope1.ServiceProvider.GetRequiredService<IAuditStore>();
        var c = scope2.ServiceProvider.GetRequiredService<IAuditStore>();

        a.Should().BeSameAs(b);
        a.Should().NotBeSameAs(c);
    }

    [Fact]
    public void AddAuditStore_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddAuditStore<FakeAuditStore>();
        result.Should().BeSameAs(services);
    }

    private sealed class FakeAuditStore : IAuditStore
    {
        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(
            DateTimeOffset? from = null, DateTimeOffset? to = null,
            int skip = 0, int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>([]);

        public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
            string dataSubjectId,
            DateTimeOffset? from = null, DateTimeOffset? to = null,
            int skip = 0, int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>([]);
    }
}
