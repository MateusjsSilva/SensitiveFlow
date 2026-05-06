using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.AspNetCore.Tests;

public sealed class SensitiveFlowAuditMiddlewareTests
{
    private static IHost BuildHost(IPseudonymizer pseudonymizer, RequestDelegate? handler = null)
    {
        return new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(pseudonymizer);
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    app.UseMiddleware<SensitiveFlowAuditMiddleware>();
                    app.Run(handler ?? (ctx => Task.CompletedTask));
                });
            })
            .Build();
    }

    [Fact]
    public async Task Middleware_WithRemoteIp_StoresTokenInItems()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize("127.0.0.1").Returns("token-abc");

        string? capturedToken = null;

        using var host = BuildHost(pseudonymizer, ctx =>
        {
            capturedToken = ctx.Items[SensitiveFlowAuditMiddleware.IpTokenKey] as string;
            return Task.CompletedTask;
        });

        await host.StartAsync();
        var client = host.GetTestClient();
        await client.GetAsync("/");

        capturedToken.Should().Be("token-abc");
    }

    [Fact]
    public async Task Middleware_CallsPseudonymizer_WithRemoteIp()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize(Arg.Any<string>()).Returns("token-x");

        using var host = BuildHost(pseudonymizer);
        await host.StartAsync();
        var client = host.GetTestClient();
        await client.GetAsync("/");

        pseudonymizer.Received(1).Pseudonymize(Arg.Any<string>());
    }

    [Fact]
    public async Task Middleware_PassesRequestToNextMiddleware()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize(Arg.Any<string>()).Returns("tok");

        var nextCalled = false;

        using var host = BuildHost(pseudonymizer, ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        await host.StartAsync();
        var response = await host.GetTestClient().GetAsync("/");

        nextCalled.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void IpTokenKey_HasExpectedValue()
    {
        SensitiveFlowAuditMiddleware.IpTokenKey.Should().Be("SensitiveFlow.IpToken");
    }
}
