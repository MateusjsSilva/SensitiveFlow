using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Profiles;

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
}
