namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Output context for contextual redaction decisions.
/// Used to apply role-based or context-specific redaction rules.
/// </summary>
public enum RedactionContext
{
    /// <summary>Generic API response context.</summary>
    ApiResponse = 0,

    /// <summary>Application logging context.</summary>
    Log,

    /// <summary>Audit record context.</summary>
    Audit,

    /// <summary>Data export context.</summary>
    Export,

    /// <summary>Administrator view context (full access, no redaction).</summary>
    AdminView,

    /// <summary>Support/helpdesk view context (limited access, partial redaction).</summary>
    SupportView,

    /// <summary>Customer self-service view context (restricted access, heavy redaction).</summary>
    CustomerView,
}

