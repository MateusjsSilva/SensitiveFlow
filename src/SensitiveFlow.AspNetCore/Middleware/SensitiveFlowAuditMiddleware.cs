using Microsoft.AspNetCore.Http;
using SensitiveFlow.AspNetCore.Correlation;
using SensitiveFlow.AspNetCore.Diagnostics;
using SensitiveFlow.AspNetCore.IpMasking;
using SensitiveFlow.AspNetCore.Session;
using SensitiveFlow.AspNetCore.Tenant;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.AspNetCore;

/// <summary>
/// Middleware that pseudonymizes the request IP address and stores the token
/// in <see cref="HttpContext.Items"/> so <c>HttpAuditContext</c> can read it
/// without accessing raw personal data downstream.
/// </summary>
/// <remarks>
/// CORS preflight (<c>OPTIONS</c>) and well-known health/liveness paths are skipped so the
/// token store is not hammered by traffic that has no meaningful actor. Tune the skip list
/// via <see cref="SensitiveFlowAuditMiddlewareOptions"/> if your app uses non-standard paths.
/// </remarks>
public sealed class SensitiveFlowAuditMiddleware
{
    /// <summary>Key used to store the pseudonymized IP token in <see cref="HttpContext.Items"/>.</summary>
    public const string IpTokenKey = "SensitiveFlow.IpToken";

    /// <summary>Key used to store the extracted session ID in <see cref="HttpContext.Items"/>.</summary>
    public const string SessionIdKey = "SensitiveFlow.SessionId";

    /// <summary>Key used to store the correlation ID in <see cref="HttpContext.Items"/>.</summary>
    public const string CorrelationIdKey = "SensitiveFlow.CorrelationId";

    /// <summary>Key used to store the tenant ID in <see cref="HttpContext.Items"/>.</summary>
    public const string TenantIdKey = "SensitiveFlow.TenantId";

    private readonly RequestDelegate _next;
    private readonly SensitiveFlowAspNetCorePipelineDiagnostics? _diagnostics;
    private readonly SensitiveFlowAuditMiddlewareOptions _options;

    /// <summary>Initializes a new instance of <see cref="SensitiveFlowAuditMiddleware"/>.</summary>
    public SensitiveFlowAuditMiddleware(
        RequestDelegate next,
        SensitiveFlowAspNetCorePipelineDiagnostics? diagnostics = null,
        SensitiveFlowAuditMiddlewareOptions? options = null)
    {
        _next = next;
        _diagnostics = diagnostics;
        _options = options ?? new SensitiveFlowAuditMiddlewareOptions();
    }

    /// <summary>Pseudonymizes the remote IP and stores the token before passing to the next middleware.</summary>
    public async Task InvokeAsync(HttpContext context, IPseudonymizer pseudonymizer)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            _diagnostics?.MarkAuthenticatedUserObserved();
        }

        if (ShouldPseudonymize(context))
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(ip))
            {
                if (_options.IpMasking.Enabled)
                {
                    context.Items[IpTokenKey] = IpMaskingHelper.Mask(ip, _options.IpMasking.MaskSuffix);
                }
                else
                {
                    context.Items[IpTokenKey] = await pseudonymizer
                        .PseudonymizeAsync(ip, context.RequestAborted)
                        .ConfigureAwait(false);
                }
            }
        }

        if (_options.TrackSessionId)
        {
            context.Items[SessionIdKey] = SessionIdExtractor.Extract(context);
        }

        var correlationId = context.Request.Headers[_options.CorrelationId.HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(correlationId) && _options.CorrelationId.GenerateIfMissing)
        {
            correlationId = Guid.NewGuid().ToString("N");
        }
        if (!string.IsNullOrEmpty(correlationId))
        {
            context.Items[CorrelationIdKey] = correlationId;
        }

        var tenantId = ExtractTenantId(context);
        if (!string.IsNullOrEmpty(tenantId))
        {
            context.Items[TenantIdKey] = tenantId;
        }

        await _next(context).ConfigureAwait(false);
    }

    private string? ExtractTenantId(HttpContext context)
    {
        if (!string.IsNullOrEmpty(_options.Tenant.ClaimName))
        {
            var claimValue = context.User.FindFirst(_options.Tenant.ClaimName)?.Value;
            if (!string.IsNullOrEmpty(claimValue))
            {
                return claimValue;
            }
        }

        if (!string.IsNullOrEmpty(_options.Tenant.HeaderName))
        {
            var headerValue = context.Request.Headers[_options.Tenant.HeaderName].FirstOrDefault();
            if (!string.IsNullOrEmpty(headerValue))
            {
                return headerValue;
            }
        }

        return null;
    }

    private bool ShouldPseudonymize(HttpContext context)
    {
        // Skip CORS preflight — no actor information to capture, and these fire on every
        // cross-origin call.
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            return false;
        }

        if (_options.SkipPaths.Count == 0)
        {
            return true;
        }

        var path = context.Request.Path;
        foreach (var skip in _options.SkipPaths)
        {
            if (path.StartsWithSegments(skip, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Options controlling <see cref="SensitiveFlowAuditMiddleware"/> request-gating behavior.
/// </summary>
public sealed class SensitiveFlowAuditMiddlewareOptions
{
    /// <summary>
    /// Path prefixes (case-insensitive) for which the middleware skips IP pseudonymization.
    /// Defaults cover common health/liveness/readiness probes. Add app-specific prefixes
    /// (e.g. <c>/metrics</c>) to avoid storing tokens for synthetic traffic.
    /// </summary>
    public IList<PathString> SkipPaths { get; } = new List<PathString>
    {
        "/health",
        "/healthz",
        "/livez",
        "/readyz",
    };

    /// <summary>
    /// Gets or sets a value indicating whether session IDs should be extracted and stored.
    /// Default is <c>false</c>. Enable only if <c>AddSession()</c> is configured.
    /// </summary>
    public bool TrackSessionId { get; set; } = false;

    /// <summary>
    /// Gets or sets the options for correlation ID extraction and generation.
    /// </summary>
    public CorrelationIdOptions CorrelationId { get; set; } = new();

    /// <summary>
    /// Gets or sets the options for tenant ID extraction from claims or headers.
    /// </summary>
    public TenantIdOptions Tenant { get; set; } = new();

    /// <summary>
    /// Gets or sets the options for customizing which claims are checked for the actor ID.
    /// </summary>
    public SensitiveFlow.AspNetCore.Claims.ActorIdClaimOptions ActorId { get; set; } = new();

    /// <summary>
    /// Gets or sets the options for IP address masking.
    /// </summary>
    public IpMaskingOptions IpMasking { get; set; } = new();
}
