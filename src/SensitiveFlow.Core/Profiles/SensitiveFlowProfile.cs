namespace SensitiveFlow.Core.Profiles;

/// <summary>
/// Built-in handling profiles that provide a quick starting point for SensitiveFlow options.
/// </summary>
public enum SensitiveFlowProfile
{
    /// <summary>Developer-friendly defaults with masking and lightweight audit hints.</summary>
    Development = 0,

    /// <summary>Balanced defaults suitable for most API and logging surfaces.</summary>
    Balanced,

    /// <summary>Strict defaults that prefer omission/redaction and required audit.</summary>
    Strict,

    /// <summary>Audit-focused defaults that do not force JSON/log redaction decisions.</summary>
    AuditOnly,
}
