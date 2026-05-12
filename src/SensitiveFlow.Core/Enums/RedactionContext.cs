namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Output context for contextual redaction decisions.
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
}

