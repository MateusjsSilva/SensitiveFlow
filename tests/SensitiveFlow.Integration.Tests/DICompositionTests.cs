using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SensitiveFlow.Anonymization.Erasure;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Audit.EFCore.Maintenance;
using SensitiveFlow.Audit.EFCore.Extensions;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Diagnostics.Extensions;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.EFCore.Interceptors;
using SensitiveFlow.Logging.Extensions;
using SensitiveFlow.Logging.Redaction;
using SensitiveFlow.Retention.Extensions;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Integration.Tests;

/// <summary>
/// Validates that the public DI extensions compose correctly. These are smoke tests:
/// any user that copies the README's "register everything" snippet must be able to
/// build a service provider with <c>ValidateScopes = true</c> and resolve every public
/// contract without exceptions.
/// </summary>
public sealed class DICompositionTests
{
    [Fact]
    public void FullStack_WithEfCoreAuditStore_ResolvesEveryPublicContract()
    {
        var services = new ServiceCollection();

        services.AddEfCoreAuditStore(opt => opt.UseInMemoryDatabase("audit-stack"));
        services.AddSensitiveFlowEFCore();
        services.AddSensitiveFlowLogging();
        services.AddRetention();
        services.AddDataSubjectErasure();

        var provider = services.BuildServiceProvider(validateScopes: true);

        // Singletons can be resolved from the root.
        provider.GetRequiredService<IAuditStore>().Should().NotBeNull();
        provider.GetRequiredService<IAuditLogRetention>().Should().NotBeNull();
        provider.GetRequiredService<ISensitiveValueRedactor>().Should().NotBeNull();
        provider.GetRequiredService<IDataSubjectErasureService>().Should().NotBeNull();
        provider.GetRequiredService<IErasureStrategy>().Should().NotBeNull();

        // Scoped services must resolve from a scope.
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuditContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<SensitiveDataAuditInterceptor>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<RetentionEvaluator>().Should().NotBeNull();
    }

    [Fact]
    public void FullStack_WithRetryDecorator_ResolvesAuditStoreAsRetryingDecorator()
    {
        var services = new ServiceCollection();

        services.AddEfCoreAuditStore(opt => opt.UseInMemoryDatabase("retry-stack"));
        services.AddAuditStoreRetry();
        services.AddSensitiveFlowEFCore();

        var provider = services.BuildServiceProvider(validateScopes: true);

        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<RetryingAuditStore>();
    }

    [Fact]
    public void FullStack_WithRetryAndDiagnostics_BothDecoratorsApplied_OuterIsLastCalled()
    {
        var services = new ServiceCollection();

        services.AddEfCoreAuditStore(opt => opt.UseInMemoryDatabase("decorators-stack"));
        services.AddAuditStoreRetry();
        services.AddSensitiveFlowDiagnostics();
        services.AddSensitiveFlowEFCore();

        var provider = services.BuildServiceProvider(validateScopes: true);

        // The last decorator applied should be the outermost — diagnostics here.
        var store = provider.GetRequiredService<IAuditStore>();
        store.GetType().Name.Should().Be("InstrumentedAuditStore");
    }

    [Fact]
    public void AspNetCore_AddedAfterEFCore_ReplacesNullAuditContext()
    {
        var services = new ServiceCollection();

        services.AddEfCoreAuditStore(opt => opt.UseInMemoryDatabase("aspnet-stack"));
        services.AddSensitiveFlowEFCore();
        services.AddSensitiveFlowAspNetCore();

        var provider = services.BuildServiceProvider(validateScopes: true);

        // HttpAuditContext is scoped — must resolve from a scope.
        using var scope = provider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        ctx.GetType().Name.Should().Be("HttpAuditContext");
    }

    [Fact]
    public void AddAuditStoreRetry_BeforeAddAuditStore_ThrowsHelpfulException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddAuditStoreRetry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IAuditStore*");
    }

    [Fact]
    public void AddSensitiveFlowDiagnostics_BeforeAddAuditStore_ThrowsHelpfulException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddSensitiveFlowDiagnostics();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IAuditStore*");
    }

    [Fact]
    public void AddTokenStore_RegistersBothTokenStoreAndPseudonymizer()
    {
        var services = new ServiceCollection();

        services.AddTokenStore<FakeTokenStore>();

        var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITokenStore>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IPseudonymizer>().Should().NotBeNull();
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
            => Task.FromResult("tok");
        public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }
}
