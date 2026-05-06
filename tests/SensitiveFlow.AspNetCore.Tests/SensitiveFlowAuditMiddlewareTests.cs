using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.AspNetCore.Tests;

public sealed class SensitiveFlowAuditMiddlewareTests
{
    private static DefaultHttpContext MakeContext(string? remoteIp = "127.0.0.1")
    {
        var ctx = new DefaultHttpContext();
        if (remoteIp is not null)
        {
            ctx.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        }
        return ctx;
    }

    [Fact]
    public async Task Middleware_WithRemoteIp_StoresTokenInItems()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize("192.168.1.42").Returns("token-abc");

        var httpContext = MakeContext("192.168.1.42");

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask, pseudonymizer);
        await middleware.InvokeAsync(httpContext);

        httpContext.Items[SensitiveFlowAuditMiddleware.IpTokenKey].Should().Be("token-abc");
    }

    [Fact]
    public async Task Middleware_CallsPseudonymizer_WithRemoteIp()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize(Arg.Any<string>()).Returns("token-x");

        var httpContext = MakeContext("10.0.0.1");

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask, pseudonymizer);
        await middleware.InvokeAsync(httpContext);

        pseudonymizer.Received(1).Pseudonymize("10.0.0.1");
    }

    [Fact]
    public async Task Middleware_NullRemoteIp_DoesNotStoreToken()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();

        var httpContext = MakeContext(null);

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask, pseudonymizer);
        await middleware.InvokeAsync(httpContext);

        httpContext.Items.Should().NotContainKey(SensitiveFlowAuditMiddleware.IpTokenKey);
        pseudonymizer.DidNotReceive().Pseudonymize(Arg.Any<string>());
    }

    [Fact]
    public async Task Middleware_AlwaysCallsNext()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.Pseudonymize(Arg.Any<string>()).Returns("tok");

        var nextCalled = false;
        var httpContext = MakeContext();

        var middleware = new SensitiveFlowAuditMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, pseudonymizer);

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_NullIp_StillCallsNext()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        var nextCalled = false;
        var httpContext = MakeContext(null);

        var middleware = new SensitiveFlowAuditMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, pseudonymizer);

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void IpTokenKey_HasExpectedValue()
    {
        SensitiveFlowAuditMiddleware.IpTokenKey.Should().Be("SensitiveFlow.IpToken");
    }
}
