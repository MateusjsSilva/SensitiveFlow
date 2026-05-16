using Microsoft.AspNetCore.Http;
using SensitiveFlow.Json.Enums;

namespace SensitiveFlow.AspNetCore.EFCore.HeaderControl;

/// <summary>
/// Extracts and parses client-provided redaction level from request headers.
/// </summary>
public static class RedactionLevelHeader
{
    /// <summary>
    /// Default header name for client-provided redaction level.
    /// </summary>
    public const string DefaultHeaderName = "X-Redaction-Level";

    /// <summary>
    /// Extracts the redaction level from the request headers.
    /// </summary>
    public static JsonRedactionMode? TryExtractFromHeaders(HttpRequest request, string headerName = DefaultHeaderName)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(headerName);

        if (!request.Headers.TryGetValue(headerName, out var headerValue))
        {
            return null;
        }

        var value = headerValue.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<JsonRedactionMode>(value, ignoreCase: true, out var mode))
        {
            return mode;
        }

        return null;
    }

    /// <summary>
    /// Stores the parsed redaction level in HttpContext.Items for later retrieval.
    /// </summary>
    public static void StoreInContext(HttpContext context, JsonRedactionMode? mode, string key = "SensitiveFlow.RedactionLevel")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);

        if (mode.HasValue)
        {
            context.Items[key] = mode.Value;
        }
    }

    /// <summary>
    /// Retrieves the stored redaction level from HttpContext.Items.
    /// </summary>
    public static JsonRedactionMode? TryGetFromContext(HttpContext context, string key = "SensitiveFlow.RedactionLevel")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);

        if (context.Items.TryGetValue(key, out var value) && value is JsonRedactionMode mode)
        {
            return mode;
        }

        return null;
    }
}
