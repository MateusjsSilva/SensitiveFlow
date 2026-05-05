namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Legal bases for processing sensitive personal data under applicable privacy regulations.
/// </summary>
public enum SensitiveLegalBasis
{
    /// <summary>specific and prominent consent from the data subject.</summary>
    ExplicitConsent = 1,

    /// <summary>compliance with a legal or regulatory obligation.</summary>
    LegalObligation = 2,

    /// <summary>shared processing for public policy execution.</summary>
    PublicPolicy = 3,

    /// <summary>studies by a research body with anonymization whenever possible.</summary>
    Research = 4,

    /// <summary>exercise of rights.</summary>
    ExerciseOfRights = 5,

    /// <summary>protection of life or physical safety.</summary>
    ProtectionOfLife = 6,

    /// <summary>health protection by professionals or health authorities.</summary>
    HealthProtection = 7,

    /// <summary>fraud prevention and data subject security safeguards.</summary>
    FraudPrevention = 8
}


