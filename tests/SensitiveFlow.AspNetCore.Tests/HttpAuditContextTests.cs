using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using SensitiveFlow.AspNetCore.Context;

namespace SensitiveFlow.AspNetCore.Tests;

public sealed class HttpAuditContextTests
{
    private static HttpAuditContext MakeContext(HttpContext? httpContext)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new HttpAuditContext(accessor);
    }

    [Fact]
    public void ActorId_ReturnsSubClaim_WhenPresent()
    {
        var claims = new[] { new Claim("sub", "user-123") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var context = MakeContext(httpContext);

        context.ActorId.Should().Be("user-123");
    }

    [Fact]
    public void ActorId_FallsBackToNameIdentifier_WhenSubMappedToNameIdentifier()
    {
        // §4.3.6: JwtBearer's default MapInboundClaims=true renames "sub" to NameIdentifier.
        // The context must still resolve the actor in that case.
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-456") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var context = MakeContext(httpContext);

        context.ActorId.Should().Be("user-456");
    }

    [Fact]
    public void ActorId_FallsBackToIdentityName_WhenNoSubClaim()
    {
        var identity = new ClaimsIdentity([], "test");
        identity.AddClaim(new Claim(ClaimTypes.Name, "alice"));
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var context = MakeContext(httpContext);

        context.ActorId.Should().Be("alice");
    }

    [Fact]
    public void ActorId_ReturnsNull_WhenNoHttpContext()
    {
        var context = MakeContext(null);
        context.ActorId.Should().BeNull();
    }

    [Fact]
    public void ActorId_ReturnsNull_WhenNoClaims()
    {
        var httpContext = new DefaultHttpContext();
        var context = MakeContext(httpContext);

        context.ActorId.Should().BeNull();
    }

    [Fact]
    public void IpAddressToken_ReturnsToken_WhenPresentInItems()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[SensitiveFlowAuditMiddleware.IpTokenKey] = "token-xyz";

        var context = MakeContext(httpContext);

        context.IpAddressToken.Should().Be("token-xyz");
    }

    [Fact]
    public void IpAddressToken_ReturnsNull_WhenNotInItems()
    {
        var httpContext = new DefaultHttpContext();
        var context = MakeContext(httpContext);

        context.IpAddressToken.Should().BeNull();
    }

    [Fact]
    public void IpAddressToken_ReturnsNull_WhenNoHttpContext()
    {
        var context = MakeContext(null);
        context.IpAddressToken.Should().BeNull();
    }
}
