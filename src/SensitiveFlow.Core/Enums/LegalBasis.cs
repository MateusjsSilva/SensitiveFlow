namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Legal bases for processing personal data under applicable privacy regulations.
/// </summary>
public enum LegalBasis
{
    /// <summary>consent of the data subject.</summary>
    Consent = 1,

    /// <summary>compliance with a legal or regulatory obligation.</summary>
    LegalObligation = 2,

    /// <summary>public administration for public policies.</summary>
    PublicPolicy = 3,

    /// <summary>studies by a research body.</summary>
    Research = 4,

    /// <summary>performance of a contract or pre-contractual procedures.</summary>
    ContractPerformance = 5,

    /// <summary>exercise of rights in judicial, administrative, or arbitration proceedings.</summary>
    ExerciseOfRights = 6,

    /// <summary>protection of life or physical safety of the data subject or third parties.</summary>
    ProtectionOfLife = 7,

    /// <summary>health protection, exclusively by health professionals.</summary>
    HealthProtection = 8,

    /// <summary>legitimate interests of the controller or third parties.</summary>
    LegitimateInterest = 9,

    /// <summary>credit protection.</summary>
    CreditProtection = 10
}


