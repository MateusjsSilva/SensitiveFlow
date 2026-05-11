namespace SensitiveFlow.Core.Policies;

/// <summary>
/// Handling actions that can be requested by a SensitiveFlow policy.
/// </summary>
[Flags]
public enum SensitiveFlowPolicyAction
{
    /// <summary>No behavior is requested.</summary>
    None = 0,

    /// <summary>Mask values before writing them to logs.</summary>
    MaskInLogs = 1 << 0,

    /// <summary>Redact values before serializing them as JSON.</summary>
    RedactInJson = 1 << 1,

    /// <summary>Omit values from JSON output.</summary>
    OmitInJson = 1 << 2,

    /// <summary>Record audit entries when values change.</summary>
    AuditOnChange = 1 << 3,

    /// <summary>Require audit support before processing this category.</summary>
    RequireAudit = 1 << 4,
}

