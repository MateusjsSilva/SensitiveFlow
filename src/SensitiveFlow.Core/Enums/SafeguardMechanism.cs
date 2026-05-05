namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Safeguards for international data transfers under applicable regulations.
/// </summary>
public enum SafeguardMechanism
{
    /// <summary>Country with an adequate level of protection recognized by a competent authority.</summary>
    AdequacyDecision,

    /// <summary>Contractual clauses or equivalent contractual safeguards .</summary>
    ContractualClauses,

    /// <summary>Binding corporate rules .</summary>
    BindingCorporateRules,

    /// <summary>Seals, certifications, and codes of conduct .</summary>
    SealsAndCertifications,

    /// <summary>Specific consent from the data subject .</summary>
    Consent
}


