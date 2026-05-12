using Microsoft.AspNetCore.Http;
using SensitiveFlow.AspNetCore.Diagnostics;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.AspNetCore;

/// <summary>
/// Middleware that pseudonymizes the request IP address and stores the token
/// in <see cref="HttpContext.Items"/> so <c>HttpAuditContext</c> can read it
/// without accessing raw personal data downstream.
/// </summary>
public sealed class SensitiveFlowAuditMiddleware
{
    /// <summary>Key used to store the pseudonymized IP token in <see cref="HttpContext.Items"/>.</summary>
    public const string IpTokenKey = "SensitiveFlow.IpToken";

    private readonly RequestDelegate _next;
    private readonly SensitiveFlowAspNetCorePipelineDiagnostics? _diagnostics;

    /// <summary>Initializes a new instance of <see cref="SensitiveFlowAuditMiddleware"/>.</summary>
    public SensitiveFlowAuditMiddleware(
        RequestDelegate next,
        SensitiveFlowAspNetCorePipelineDiagnostics? diagnostics = null)
    {
        _next = next;
        _diagnostics = diagnostics;
    }

    /// <summary>Pseudonymizes the remote IP and stores the token before passing to the next middleware.</summary>
    public async Task InvokeAsync(HttpContext context, IPseudonymizer pseudonymizer)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            _diagnostics?.MarkAuthenticatedUserObserved();
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ip))
        {
            context.Items[IpTokenKey] = await pseudonymizer.PseudonymizeAsync(
                ip,
                context.RequestAborted);
        }

        await _next(context);
    }
}
