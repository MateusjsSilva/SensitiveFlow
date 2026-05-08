using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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

    /// <summary>Initializes a new instance of <see cref="HttpAuditContext"/>.</summary>
    public HttpAuditContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Resolution order: raw <c>sub</c> claim → <see cref="ClaimTypes.NameIdentifier"/>
    /// (Microsoft's default mapping for OIDC <c>sub</c>) → <see cref="System.Security.Principal.IIdentity.Name"/>.
    /// This double-check exists because <c>JwtBearerOptions.MapInboundClaims</c> defaults to <see langword="true"/>,
    /// which renames <c>sub</c> to <see cref="ClaimTypes.NameIdentifier"/> before claims reach the principal.
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

            return user.FindFirst("sub")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.Identity?.Name;
        }
    }

    /// <inheritdoc />
    public virtual string? IpAddressToken =>
        _httpContextAccessor.HttpContext?.Items[SensitiveFlowAuditMiddleware.IpTokenKey] as string;
}
