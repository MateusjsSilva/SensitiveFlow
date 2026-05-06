using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using SensitiveFlow.AspNetCore.Context;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.AspNetCore.Tests;

public sealed class AspNetCoreExtensionsTests
{
    [Fact]
    public void AddSensitiveFlowAspNetCore_RegistersHttpAuditContext()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddSensitiveFlowAspNetCore();

        var provider = services.BuildServiceProvider();

        provider.GetService<IAuditContext>().Should().BeOfType<HttpAuditContext>();
    }

    [Fact]
    public void AddSensitiveFlowAspNetCore_RegistersHttpContextAccessor()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddSensitiveFlowAspNetCore();

        var provider = services.BuildServiceProvider();

        provider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Should().NotBeNull();
    }

    [Fact]
    public async Task UseSensitiveFlowAudit_AddsMiddlewareTopipeline()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize(Arg.Any<string>()).Returns("tok");

        var middlewareInvoked = false;

        using var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(pseudonymizer);
                });
                web.Configure(app =>
                {
                    app.UseSensitiveFlowAudit();
                    app.Run(ctx =>
                    {
                        middlewareInvoked = true;
                        return Task.CompletedTask;
                    });
                });
            })
            .Build();

        await host.StartAsync();
        await host.GetTestClient().GetAsync("/");

        middlewareInvoked.Should().BeTrue();
    }
}
