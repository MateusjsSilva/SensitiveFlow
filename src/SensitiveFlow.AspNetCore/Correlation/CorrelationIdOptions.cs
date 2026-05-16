namespace SensitiveFlow.AspNetCore.Correlation;

/// <summary>
/// Options for reading and managing request correlation IDs.
/// </summary>
public sealed class CorrelationIdOptions
{
    /// <summary>
    /// Gets or sets the HTTP header name to read the correlation ID from.
    /// Default is <c>"X-Correlation-ID"</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets a value indicating whether a correlation ID should be generated
    /// if not present in the request header.
    /// Default is <c>true</c>.
    /// </summary>
    public bool GenerateIfMissing { get; set; } = true;
}
