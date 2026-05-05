namespace LGPD.NET.Core.Enums;

/// <summary>
/// Safeguards for international data transfers under Art. 33 of the LGPD.
/// </summary>
public enum SafeguardMechanism
{
    /// <summary>Country with an adequate level of protection recognized by the ANPD (Art. 33, I).</summary>
    AdequacyDecision,

    /// <summary>Standard contractual clauses (Art. 33, II).</summary>
    StandardContractualClauses,

    /// <summary>Binding corporate rules (Art. 33, III).</summary>
    BindingCorporateRules,

    /// <summary>Seals, certifications, and codes of conduct (Art. 33, IV).</summary>
    SealsAndCertifications,

    /// <summary>Specific consent from the data subject (Art. 33, VIII).</summary>
    Consent
}
