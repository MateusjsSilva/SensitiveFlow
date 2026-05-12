namespace SensitiveFlow.Core.Profiles;

/// <summary>
/// Public defaults used by SensitiveFlow packages when callers do not provide
/// explicit options.
/// </summary>
public static class SensitiveFlowDefaults
{
    /// <summary>
    /// Default profile used when callers do not select one explicitly.
    /// The default is <see cref="SensitiveFlowProfile.Balanced"/>.
    /// </summary>
    public const SensitiveFlowProfile Profile = SensitiveFlowProfile.Balanced;

    /// <summary>
    /// Default replacement marker used by JSON and logging redaction.
    /// The default value is <c>[REDACTED]</c>.
    /// </summary>
    public const string RedactedPlaceholder = "[REDACTED]";

    /// <summary>
    /// Default marker used when retention anonymization replaces a string value.
    /// The default value is <c>[ANONYMIZED]</c>.
    /// </summary>
    public const string AnonymousValue = "[ANONYMIZED]";

    /// <summary>
    /// Default health-check registration name for audit-store checks.
    /// </summary>
    public const string AuditStoreHealthCheckName = "sensitiveflow-audit-store";

    /// <summary>
    /// Default health-check registration name for token-store checks.
    /// </summary>
    public const string TokenStoreHealthCheckName = "sensitiveflow-token-store";

    /// <summary>
    /// Default health-check registration name for audit-outbox checks.
    /// </summary>
    public const string AuditOutboxHealthCheckName = "sensitiveflow-audit-outbox";
}
