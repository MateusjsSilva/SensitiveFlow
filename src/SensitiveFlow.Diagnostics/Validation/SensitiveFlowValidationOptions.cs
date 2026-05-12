namespace SensitiveFlow.Diagnostics.Validation;

/// <summary>
/// Options controlling startup validation expectations.
/// </summary>
public sealed class SensitiveFlowValidationOptions
{
    /// <summary>Gets or sets whether a durable audit store is required.</summary>
    public bool RequireAuditStore { get; set; }

    /// <summary>Gets or sets whether a token store is required.</summary>
    public bool RequireTokenStore { get; set; }

    /// <summary>Gets or sets whether JSON redaction configuration is required.</summary>
    public bool RequireJsonRedaction { get; set; }

    /// <summary>Gets or sets whether retention services are required.</summary>
    public bool RequireRetention { get; set; }

    /// <summary>
    /// When <c>true</c>, <see cref="SensitiveFlowValidationOptions"/>-driven validation
    /// throws on diagnostics with severity <c>Error</c> instead of silently returning a report.
    /// Defaults to <c>true</c> so misconfigurations surface at startup, not at the first write.
    /// </summary>
    public bool FailOnError { get; set; } = true;
}
