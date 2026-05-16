using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SensitiveFlow.AspNetCore.Claims;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.AspNetCore.Context;

/// <summary>
/// Scoped <see cref="IAuditContext"/> implementation that reads actor and IP token
/// from the current <see cref="IHttpContextAccessor"/>.
/// Register <see cref="SensitiveFlowAuditMiddleware"/> to populate the pseudonymized
/// IP token before this context reads it.
/// </summary>
public class HttpAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SensitiveFlowAuditMiddlewareOptions _options;

    /// <summary>Initializes a new instance of <see cref="HttpAuditContext"/>.</summary>
    public HttpAuditContext(IHttpContextAccessor httpContextAccessor)
        : this(httpContextAccessor, new SensitiveFlowAuditMiddlewareOptions())
    {
    }

    /// <summary>Initializes a new instance of <see cref="HttpAuditContext"/> with custom middleware options.</summary>
    public HttpAuditContext(IHttpContextAccessor httpContextAccessor, SensitiveFlowAuditMiddlewareOptions options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options ?? new SensitiveFlowAuditMiddlewareOptions();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Iterates through configured claim names from <see cref="ActorIdClaimOptions.ClaimNames"/>.
    /// Falls back to <see cref="System.Security.Principal.IIdentity.Name"/> if no claims match.
    /// </remarks>
    public virtual string? ActorId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return null;
            }

            foreach (var claimName in _options.ActorId.ClaimNames)
            {
                var claimValue = user.FindFirst(claimName)?.Value;
                if (!string.IsNullOrEmpty(claimValue))
                {
                    return claimValue;
                }
            }

            return user.Identity?.Name;
        }
    }

    /// <inheritdoc />
    public virtual string? IpAddressToken =>
        _httpContextAccessor.HttpContext?.Items[SensitiveFlowAuditMiddleware.IpTokenKey] as string;

    /// <summary>
    /// Gets the session ID extracted from the HTTP request if session tracking is enabled.
    /// </summary>
    public virtual string? SessionId =>
        _httpContextAccessor.HttpContext?.Items[SensitiveFlowAuditMiddleware.SessionIdKey] as string;

    /// <summary>
    /// Gets the correlation ID for request tracing and log correlation.
    /// </summary>
    public virtual string? CorrelationId =>
        _httpContextAccessor.HttpContext?.Items[SensitiveFlowAuditMiddleware.CorrelationIdKey] as string;

    /// <summary>
    /// Gets the tenant ID extracted from claims or headers in multi-tenant scenarios.
    /// </summary>
    public virtual string? TenantId =>
        _httpContextAccessor.HttpContext?.Items[SensitiveFlowAuditMiddleware.TenantIdKey] as string;
}
