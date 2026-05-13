using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using SensitiveFlow.AspNetCore.Diagnostics;
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
        pseudonymizer.PseudonymizeAsync("192.168.1.42", Arg.Any<CancellationToken>())
            .Returns("token-abc");

        var httpContext = MakeContext("192.168.1.42");

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, pseudonymizer);

        httpContext.Items[SensitiveFlowAuditMiddleware.IpTokenKey].Should().Be("token-abc");
    }

    [Fact]
    public async Task Middleware_CallsPseudonymizer_WithRemoteIp()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.PseudonymizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("token-x");

        var httpContext = MakeContext("10.0.0.1");

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, pseudonymizer);

        await pseudonymizer.Received(1).PseudonymizeAsync("10.0.0.1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Middleware_NullRemoteIp_DoesNotStoreToken()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();

        var httpContext = MakeContext(null);

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, pseudonymizer);

        httpContext.Items.Should().NotContainKey(SensitiveFlowAuditMiddleware.IpTokenKey);
        await pseudonymizer.DidNotReceive().PseudonymizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Middleware_AlwaysCallsNext()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.PseudonymizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("tok");

        var nextCalled = false;
        var httpContext = MakeContext();

        var middleware = new SensitiveFlowAuditMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, pseudonymizer);

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
        });

        await middleware.InvokeAsync(httpContext, pseudonymizer);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void IpTokenKey_HasExpectedValue()
    {
        SensitiveFlowAuditMiddleware.IpTokenKey.Should().Be("SensitiveFlow.IpToken");
    }

    [Theory]
    [InlineData("OPTIONS", "/customers")]
    [InlineData("GET", "/health")]
    [InlineData("GET", "/HEALTH/details")]
    [InlineData("GET", "/readyz")]
    public async Task Middleware_SkipsConfiguredSyntheticRequests(string method, string path)
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        var httpContext = MakeContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = path;

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, pseudonymizer);

        httpContext.Items.Should().NotContainKey(SensitiveFlowAuditMiddleware.IpTokenKey);
        await pseudonymizer.DidNotReceive().PseudonymizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Middleware_CustomEmptySkipList_AllowsHealthPath()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.PseudonymizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("token-health");
        var httpContext = MakeContext();
        httpContext.Request.Path = "/health";
        var options = new SensitiveFlowAuditMiddlewareOptions();
        options.SkipPaths.Clear();

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask, options: options);

        await middleware.InvokeAsync(httpContext, pseudonymizer);

        httpContext.Items[SensitiveFlowAuditMiddleware.IpTokenKey].Should().Be("token-health");
    }

    [Fact]
    public async Task Middleware_AuthenticatedUser_MarksDiagnostics()
    {
        var pseudonymizer = Substitute.For<IPseudonymizer>();
        pseudonymizer.PseudonymizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("token-user");
        var diagnostics = new SensitiveFlowAspNetCorePipelineDiagnostics();
        var httpContext = MakeContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"));

        var middleware = new SensitiveFlowAuditMiddleware(_ => Task.CompletedTask, diagnostics);

        await middleware.InvokeAsync(httpContext, pseudonymizer);

        diagnostics.ObservedAuthenticatedUserBeforeAuditMiddleware.Should().BeTrue();
    }
}
