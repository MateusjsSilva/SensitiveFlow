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
    public virtual string? ActorId =>
        _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    /// <inheritdoc />
    public virtual string? IpAddressToken =>
        _httpContextAccessor.HttpContext?.Items[SensitiveFlowAuditMiddleware.IpTokenKey] as string;
}
