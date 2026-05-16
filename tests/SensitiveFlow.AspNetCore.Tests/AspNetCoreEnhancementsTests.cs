using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;
using SensitiveFlow.AspNetCore.Claims;
using SensitiveFlow.AspNetCore.Context;
using SensitiveFlow.AspNetCore.Correlation;
using SensitiveFlow.AspNetCore.IpMasking;
using SensitiveFlow.AspNetCore.Session;
using SensitiveFlow.AspNetCore.Tenant;
using System.Security.Claims;
using Xunit;

namespace SensitiveFlow.AspNetCore.Tests;

public sealed class AspNetCoreEnhancementsTests
{
    #region SessionIdExtractor Tests

    [Fact]
    public void SessionIdExtractor_Extract_ReturnsNullWhenNoSessionFeature()
    {
        var context = new DefaultHttpContext();
        var result = SessionIdExtractor.Extract(context);

        result.Should().BeNull();
    }

    [Fact]
    public void SessionIdExtractor_Extract_ReturnsSessionIdWhenAvailable()
    {
        var context = new DefaultHttpContext();
        var mockSession = Substitute.For<ISession>();
        mockSession.Id.Returns("session-123");

        var features = context.Features;
        var sessionFeature = Substitute.For<ISessionFeature>();
        sessionFeature.Session.Returns(mockSession);
        features.Set(sessionFeature);

        var result = SessionIdExtractor.Extract(context);

        result.Should().Be("session-123");
    }

    [Fact]
    public void SessionIdExtractor_Extract_ThrowsOnNullContext()
    {
        var act = () => SessionIdExtractor.Extract(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region CorrelationIdOptions Tests

    [Fact]
    public void CorrelationIdOptions_DefaultHeaderName()
    {
        var options = new CorrelationIdOptions();

        options.HeaderName.Should().Be("X-Correlation-ID");
    }

    [Fact]
    public void CorrelationIdOptions_DefaultGenerateIfMissing()
    {
        var options = new CorrelationIdOptions();

        options.GenerateIfMissing.Should().BeTrue();
    }

    [Fact]
    public void CorrelationIdOptions_CanBeConfigured()
    {
        var options = new CorrelationIdOptions
        {
            HeaderName = "X-Custom-Correlation",
            GenerateIfMissing = false,
        };

        options.HeaderName.Should().Be("X-Custom-Correlation");
        options.GenerateIfMissing.Should().BeFalse();
    }

    #endregion

    #region TenantIdOptions Tests

    [Fact]
    public void TenantIdOptions_DefaultClaimName()
    {
        var options = new TenantIdOptions();

        options.ClaimName.Should().Be("tid");
    }

    [Fact]
    public void TenantIdOptions_DefaultHeaderName()
    {
        var options = new TenantIdOptions();

        options.HeaderName.Should().Be("X-Tenant-ID");
    }

    [Fact]
    public void TenantIdOptions_CanBeConfigured()
    {
        var options = new TenantIdOptions
        {
            ClaimName = "tenant",
            HeaderName = "X-Custom-Tenant",
        };

        options.ClaimName.Should().Be("tenant");
        options.HeaderName.Should().Be("X-Custom-Tenant");
    }

    [Fact]
    public void TenantIdOptions_CanDisableClaim()
    {
        var options = new TenantIdOptions { ClaimName = null };

        options.ClaimName.Should().BeNull();
    }

    #endregion

    #region ActorIdClaimOptions Tests

    [Fact]
    public void ActorIdClaimOptions_DefaultClaimNames()
    {
        var options = new ActorIdClaimOptions();

        options.ClaimNames.Should().Contain("sub");
        options.ClaimNames.Should().Contain(ClaimTypes.NameIdentifier);
    }

    [Fact]
    public void ActorIdClaimOptions_CanBeConfigured()
    {
        var options = new ActorIdClaimOptions
        {
            ClaimNames = new[] { "oid", "custom-id" }.ToList(),
        };

        options.ClaimNames.Should().Contain("oid");
        options.ClaimNames.Should().Contain("custom-id");
    }

    #endregion

    #region IpMaskingHelper Tests

    [Fact]
    public void IpMaskingHelper_Mask_MasksIPv4LastOctet()
    {
        var result = IpMaskingHelper.Mask("192.168.1.42", "XXX");

        result.Should().Be("192.168.1.XXX");
    }

    [Fact]
    public void IpMaskingHelper_Mask_UsesCustomSuffix()
    {
        var result = IpMaskingHelper.Mask("192.168.1.42", "***");

        result.Should().Be("192.168.1.***");
    }

    [Fact]
    public void IpMaskingHelper_Mask_HandlesIPv6()
    {
        var result = IpMaskingHelper.Mask("fe80::1", "XXXX");

        result.Should().Be("fe80::XXXX");
    }

    [Fact]
    public void IpMaskingHelper_Mask_ReturnsUnchangedForInvalidInput()
    {
        var result = IpMaskingHelper.Mask("not-an-ip", "XXX");

        result.Should().Be("not-an-ip");
    }

    [Fact]
    public void IpMaskingHelper_Mask_ThrowsOnNullInput()
    {
        var act = () => IpMaskingHelper.Mask(null!, "XXX");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IpMaskingHelper_Mask_ThrowsOnNullSuffix()
    {
        var act = () => IpMaskingHelper.Mask("192.168.1.1", null!);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region IpMaskingOptions Tests

    [Fact]
    public void IpMaskingOptions_DefaultEnabled()
    {
        var options = new IpMaskingOptions();

        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void IpMaskingOptions_DefaultMaskSuffix()
    {
        var options = new IpMaskingOptions();

        options.MaskSuffix.Should().Be("XXX");
    }

    [Fact]
    public void IpMaskingOptions_CanBeConfigured()
    {
        var options = new IpMaskingOptions
        {
            Enabled = true,
            MaskSuffix = "***",
        };

        options.Enabled.Should().BeTrue();
        options.MaskSuffix.Should().Be("***");
    }

    #endregion

    #region SensitiveFlowAuditMiddlewareOptions Tests

    [Fact]
    public void MiddlewareOptions_TrackSessionIdDefaultsToFalse()
    {
        var options = new SensitiveFlowAuditMiddlewareOptions();

        options.TrackSessionId.Should().BeFalse();
    }

    [Fact]
    public void MiddlewareOptions_HasCorrelationIdOptions()
    {
        var options = new SensitiveFlowAuditMiddlewareOptions();

        options.CorrelationId.Should().NotBeNull();
        options.CorrelationId.HeaderName.Should().Be("X-Correlation-ID");
    }

    [Fact]
    public void MiddlewareOptions_HasTenantOptions()
    {
        var options = new SensitiveFlowAuditMiddlewareOptions();

        options.Tenant.Should().NotBeNull();
        options.Tenant.ClaimName.Should().Be("tid");
    }

    [Fact]
    public void MiddlewareOptions_HasActorIdOptions()
    {
        var options = new SensitiveFlowAuditMiddlewareOptions();

        options.ActorId.Should().NotBeNull();
        options.ActorId.ClaimNames.Should().Contain("sub");
    }

    [Fact]
    public void MiddlewareOptions_HasIpMaskingOptions()
    {
        var options = new SensitiveFlowAuditMiddlewareOptions();

        options.IpMasking.Should().NotBeNull();
        options.IpMasking.Enabled.Should().BeFalse();
    }

    #endregion

    #region HttpAuditContext Custom Claim Tests

    [Fact]
    public void HttpAuditContext_ActorId_UsesConfiguredClaimNames()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim("custom-id", "custom-123"),
        };
        var identity = new ClaimsIdentity(claims);
        context.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(context);

        var options = new SensitiveFlowAuditMiddlewareOptions
        {
            ActorId = new ActorIdClaimOptions { ClaimNames = new[] { "custom-id" }.ToList() },
        };

        var auditContext = new HttpAuditContext(httpContextAccessor, options);
        var result = auditContext.ActorId;

        result.Should().Be("custom-123");
    }

    [Fact]
    public void HttpAuditContext_ActorId_FallsBackToIdentityName()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "fallback-name") });
        context.User = new ClaimsPrincipal(identity);
        httpContextAccessor.HttpContext.Returns(context);

        var options = new SensitiveFlowAuditMiddlewareOptions
        {
            ActorId = new ActorIdClaimOptions { ClaimNames = new[] { "nonexistent" }.ToList() },
        };

        var auditContext = new HttpAuditContext(httpContextAccessor, options);
        var result = auditContext.ActorId;

        result.Should().Be("fallback-name");
    }

    #endregion

    #region HttpAuditContext New Properties Tests

    [Fact]
    public void HttpAuditContext_SessionId_ReadsFromItems()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Items[SensitiveFlowAuditMiddleware.SessionIdKey] = "session-456";
        httpContextAccessor.HttpContext.Returns(context);

        var auditContext = new HttpAuditContext(httpContextAccessor);
        var result = auditContext.SessionId;

        result.Should().Be("session-456");
    }

    [Fact]
    public void HttpAuditContext_CorrelationId_ReadsFromItems()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Items[SensitiveFlowAuditMiddleware.CorrelationIdKey] = "corr-789";
        httpContextAccessor.HttpContext.Returns(context);

        var auditContext = new HttpAuditContext(httpContextAccessor);
        var result = auditContext.CorrelationId;

        result.Should().Be("corr-789");
    }

    [Fact]
    public void HttpAuditContext_TenantId_ReadsFromItems()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Items[SensitiveFlowAuditMiddleware.TenantIdKey] = "tenant-abc";
        httpContextAccessor.HttpContext.Returns(context);

        var auditContext = new HttpAuditContext(httpContextAccessor);
        var result = auditContext.TenantId;

        result.Should().Be("tenant-abc");
    }

    [Fact]
    public void HttpAuditContext_ReturnsNullWhenItemsNotSet()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        httpContextAccessor.HttpContext.Returns(context);

        var auditContext = new HttpAuditContext(httpContextAccessor);

        auditContext.SessionId.Should().BeNull();
        auditContext.CorrelationId.Should().BeNull();
        auditContext.TenantId.Should().BeNull();
    }

    #endregion

    #region SensitiveFlowAuditMiddleware Constants Tests

    [Fact]
    public void Middleware_HasSessionIdKey()
    {
        SensitiveFlowAuditMiddleware.SessionIdKey.Should().Be("SensitiveFlow.SessionId");
    }

    [Fact]
    public void Middleware_HasCorrelationIdKey()
    {
        SensitiveFlowAuditMiddleware.CorrelationIdKey.Should().Be("SensitiveFlow.CorrelationId");
    }

    [Fact]
    public void Middleware_HasTenantIdKey()
    {
        SensitiveFlowAuditMiddleware.TenantIdKey.Should().Be("SensitiveFlow.TenantId");
    }

    [Fact]
    public void Middleware_StillHasIpTokenKey()
    {
        SensitiveFlowAuditMiddleware.IpTokenKey.Should().Be("SensitiveFlow.IpToken");
    }

    #endregion
}
