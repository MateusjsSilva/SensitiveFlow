using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.Context;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.EFCore.Interceptors;

namespace SensitiveFlow.EFCore.Tests;

public sealed class EFCoreExtensionsTests
{
    [Fact]
    public void AddSensitiveFlowEFCore_RegistersInterceptor()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAuditStore>());
        services.AddSensitiveFlowEFCore();

        var provider = services.BuildServiceProvider();

        provider.GetService<SensitiveDataAuditInterceptor>().Should().NotBeNull();
    }

    [Fact]
    public void AddSensitiveFlowEFCore_RegistersNullAuditContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAuditStore>());
        services.AddSensitiveFlowEFCore();

        var provider = services.BuildServiceProvider();

        provider.GetService<IAuditContext>().Should().BeSameAs(NullAuditContext.Instance);
    }

    [Fact]
    public void AddSensitiveFlowAuditContext_ReplacesAuditContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IAuditStore>());
        services.AddSensitiveFlowEFCore();
        services.AddSensitiveFlowAuditContext<FakeAuditContext>();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetService<IAuditContext>().Should().BeOfType<FakeAuditContext>();
    }

    [Fact]
    public void NullAuditContext_ActorId_IsNull()
    {
        NullAuditContext.Instance.ActorId.Should().BeNull();
    }

    [Fact]
    public void NullAuditContext_IpAddressToken_IsNull()
    {
        NullAuditContext.Instance.IpAddressToken.Should().BeNull();
    }

    [Fact]
    public void NullAuditContext_Instance_IsSingleton()
    {
        NullAuditContext.Instance.Should().BeSameAs(NullAuditContext.Instance);
    }

    private sealed class FakeAuditContext : IAuditContext
    {
        public string? ActorId => "fake-actor";
        public string? IpAddressToken => null;
    }
}
