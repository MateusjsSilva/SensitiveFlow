namespace LGPD.NET.Core.Enums;

/// <summary>
/// Legal bases for processing personal data under Art. 7 of the LGPD.
/// </summary>
public enum LegalBasis
{
    /// <summary>Art. 7, I - consent of the data subject.</summary>
    Consent = 1,

    /// <summary>Art. 7, II - compliance with a legal or regulatory obligation.</summary>
    LegalObligation = 2,

    /// <summary>Art. 7, III - public administration for public policies.</summary>
    PublicPolicy = 3,

    /// <summary>Art. 7, IV - studies by a research body.</summary>
    Research = 4,

    /// <summary>Art. 7, V - performance of a contract or pre-contractual procedures.</summary>
    ContractPerformance = 5,

    /// <summary>Art. 7, VI - exercise of rights in judicial, administrative, or arbitration proceedings.</summary>
    ExerciseOfRights = 6,

    /// <summary>Art. 7, VII - protection of life or physical safety of the data subject or third parties.</summary>
    ProtectionOfLife = 7,

    /// <summary>Art. 7, VIII - health protection, exclusively by health professionals.</summary>
    HealthProtection = 8,

    /// <summary>Art. 7, IX - legitimate interests of the controller or third parties.</summary>
    LegitimateInterest = 9,

    /// <summary>Art. 7, X - credit protection.</summary>
    CreditProtection = 10
}
