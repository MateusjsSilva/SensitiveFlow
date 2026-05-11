namespace SensitiveFlow.Core.Profiles;

/// <summary>
/// Documented defaults shared across SensitiveFlow packages.
/// </summary>
public static class SensitiveFlowDefaults
{
    /// <summary>Default profile used when callers do not select one explicitly.</summary>
    public const SensitiveFlowProfile Profile = SensitiveFlowProfile.Balanced;

    /// <summary>Default marker used for full redaction.</summary>
    public const string RedactedPlaceholder = "[REDACTED]";

    /// <summary>Default marker used by retention anonymization.</summary>
    public const string AnonymousValue = "[ANONYMIZED]";

    /// <summary>Default health check name for audit stores.</summary>
    public const string AuditStoreHealthCheckName = "sensitiveflow-audit-store";

    /// <summary>Default health check name for token stores.</summary>
    public const string TokenStoreHealthCheckName = "sensitiveflow-token-store";
}
