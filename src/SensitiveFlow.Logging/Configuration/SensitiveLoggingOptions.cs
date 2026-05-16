using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Logging.Masking;
using SensitiveFlow.Logging.Metrics;
using SensitiveFlow.Logging.Sampling;
using SensitiveFlow.Logging.StructuredRedaction;

namespace SensitiveFlow.Logging.Configuration;

/// <summary>
/// Options used by SensitiveFlow logging redaction.
/// </summary>
public sealed class SensitiveLoggingOptions
{
    /// <summary>
    /// Replacement marker used for explicit redaction. The default value is
    /// <see cref="SensitiveFlowDefaults.RedactedPlaceholder"/>.
    /// </summary>
    public string RedactedPlaceholder { get; set; } = SensitiveFlowDefaults.RedactedPlaceholder;

    /// <summary>
    /// Optional category policy registry. When present, properties in categories
    /// configured with <see cref="SensitiveFlowPolicyAction.MaskInLogs"/> are masked
    /// before they reach the inner logger.
    /// </summary>
    public SensitiveFlowPolicyRegistry? Policies { get; set; }

    /// <summary>
    /// Controls whether structured object values are inspected for <see cref="Core.Attributes.PersonalDataAttribute"/>
    /// and <see cref="Core.Attributes.SensitiveDataAttribute"/> annotations. When enabled, fields decorated
    /// with these attributes are automatically redacted without requiring manual <c>[Sensitive]</c> prefix
    /// markers in the log template. This provides consistency with other SensitiveFlow modules (audit, export, JSON).
    /// The default is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <c>RedactAnnotatedObjects = true</c> (default):
    /// <code>
    /// // No need for [Sensitive] prefix — auto-detected
    /// logger.LogInformation("User logged in: {User}", customerObj);
    /// // User.Email with [PersonalData] is automatically redacted
    /// </code>
    /// </para>
    /// <para>
    /// With <c>RedactAnnotatedObjects = false</c>:
    /// <code>
    /// // Must explicitly mark sensitive fields
    /// logger.LogInformation("User logged in: {[Sensitive]Email}", email);
    /// </code>
    /// </para>
    /// </remarks>
    public bool RedactAnnotatedObjects { get; set; } = true;

    /// <summary>
    /// Fallback action for annotated object members when no contextual attribute or
    /// policy rule applies. The default is <see cref="OutputRedactionAction.Redact"/>.
    /// Set to <see cref="OutputRedactionAction.Mask"/> to obscure values in place,
    /// or <see cref="OutputRedactionAction.Omit"/> to remove them from output entirely.
    /// </summary>
    public OutputRedactionAction DefaultAnnotatedMemberAction { get; set; } = OutputRedactionAction.Redact;

    /// <summary>
    /// Optional structured property redactor for dictionary-bag properties in log scopes.
    /// When configured, property names in the redactor are replaced with the redacted placeholder.
    /// </summary>
    public StructuredPropertyRedactor? StructuredPropertyRedactor { get; set; }

    /// <summary>
    /// Optional metrics collector for tracking redaction operations.
    /// When configured, metrics are recorded for every redaction applied.
    /// </summary>
    public IRedactionMetricsCollector? MetricsCollector { get; set; }

    /// <summary>
    /// Registry of custom masking strategies for flexible field masking.
    /// When configured, strategies can be referenced in log attributes.
    /// </summary>
    public MaskingStrategyRegistry? MaskingStrategies { get; set; }

    /// <summary>
    /// Optional log sampling filter for reducing log volume containing sensitive data.
    /// When configured with a rate less than 1.0, logs with redacted fields are sampled.
    /// </summary>
    public LogSamplingFilter? SamplingFilter { get; set; }
}
