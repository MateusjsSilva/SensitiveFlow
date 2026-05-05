using Microsoft.AspNetCore.Http;
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
    private readonly IPseudonymizer _pseudonymizer;

    /// <summary>Initializes a new instance of <see cref="SensitiveFlowAuditMiddleware"/>.</summary>
    public SensitiveFlowAuditMiddleware(RequestDelegate next, IPseudonymizer pseudonymizer)
    {
        _next = next;
        _pseudonymizer = pseudonymizer;
    }

    /// <summary>Pseudonymizes the remote IP and stores the token before passing to the next middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ip))
        {
            context.Items[IpTokenKey] = _pseudonymizer.Pseudonymize(ip);
        }

        await _next(context);
    }
}
