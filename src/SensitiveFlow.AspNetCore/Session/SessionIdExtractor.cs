using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace SensitiveFlow.AspNetCore.Session;

/// <summary>
/// Static utility for extracting session IDs from HTTP requests.
/// Safely handles cases where session support is not configured.
/// </summary>
public static class SessionIdExtractor
{
    /// <summary>
    /// Extracts the session ID from an HTTP context if session support is available.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The session ID if available, otherwise <c>null</c>.</returns>
    public static string? Extract(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sessionFeature = context.Features.Get<ISessionFeature>();
        return sessionFeature?.Session?.Id;
    }
}
