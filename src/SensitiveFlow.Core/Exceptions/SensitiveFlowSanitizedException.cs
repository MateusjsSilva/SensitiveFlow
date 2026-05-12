namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Privacy-safe exception view for logs and diagnostics.
/// </summary>
public sealed record SensitiveFlowSanitizedException
{
    /// <summary>Gets the exception type name.</summary>
    public required string Type { get; init; }

    /// <summary>Gets a safe error code when available.</summary>
    public string? Code { get; init; }

    /// <summary>Gets the sanitized message.</summary>
    public required string Message { get; init; }
}

