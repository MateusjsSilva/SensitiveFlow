namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Distinguishes anonymization from pseudonymization under applicable privacy regulations.
/// </summary>
public enum AnonymizationType
{
    /// <summary>
    /// Irreversible. The data is no longer personal and falls outside personal-data scope .
    /// </summary>
    Anonymization,

    /// <summary>
    /// Reversible. The data remains personal and all privacy obligations apply .
    /// </summary>
    Pseudonymization
}


