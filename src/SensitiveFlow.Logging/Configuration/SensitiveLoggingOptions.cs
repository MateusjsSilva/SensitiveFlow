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
    /// Controls whether structured object values are inspected for annotated members.
    /// The default is <c>true</c>.
    /// </summary>
    public bool RedactAnnotatedObjects { get; set; } = true;

    /// <summary>
    /// Fallback action for annotated object members when no contextual attribute or
    /// policy rule applies. The default is <see cref="OutputRedactionAction.Redact"/>.
    /// </summary>
    public OutputRedactionAction DefaultAnnotatedMemberAction { get; set; } = OutputRedactionAction.Redact;
}
