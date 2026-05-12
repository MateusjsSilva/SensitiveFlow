using Microsoft.AspNetCore.Http;
using SensitiveFlow.AspNetCore.Diagnostics;
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
                context.Items[IpTokenKey] = await pseudonymizer
                    .PseudonymizeAsync(ip, context.RequestAborted)
                    .ConfigureAwait(false);
            }
        }

        await _next(context).ConfigureAwait(false);
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
}
