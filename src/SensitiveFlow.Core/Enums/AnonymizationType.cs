namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Distinguishes anonymization from pseudonymization under Art. 12 of the LGPD.
/// </summary>
public enum AnonymizationType
{
    /// <summary>
    /// Irreversible. The data is no longer personal and falls outside the LGPD scope (Art. 12).
    /// </summary>
    Anonymization,

    /// <summary>
    /// Reversible. The data remains personal and all LGPD obligations apply (Art. 12, sec. 3).
    /// </summary>
    Pseudonymization
}

