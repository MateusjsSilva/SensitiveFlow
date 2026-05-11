using System.Text.RegularExpressions;

namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Produces safe exception messages by removing common high-risk raw values.
/// </summary>
public static partial class SensitiveFlowExceptionSanitizer
{
    /// <summary>Creates a sanitized exception view suitable for logs and diagnostics.</summary>
    public static SensitiveFlowSanitizedException Sanitize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var code = exception is SensitiveFlowException sensitiveFlowException
            ? sensitiveFlowException.Code
            : null;

        return new SensitiveFlowSanitizedException
        {
            Type = exception.GetType().Name,
            Code = code,
            Message = SanitizeMessage(exception.Message),
        };
    }

    /// <summary>Sanitizes a raw exception message.</summary>
    public static string SanitizeMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var sanitized = EmailRegex().Replace(message, "[email]");
        sanitized = LongDigitRegex().Replace(sanitized, "[number]");
        return sanitized;
    }

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d[\d.\-\/\s]{5,}\d\b", RegexOptions.CultureInvariant)]
    private static partial Regex LongDigitRegex();
}

