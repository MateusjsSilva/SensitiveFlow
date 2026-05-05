namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Legal bases for processing sensitive personal data under Art. 11 of the LGPD.
/// </summary>
public enum SensitiveLegalBasis
{
    /// <summary>Art. 11, I - specific and prominent consent from the data subject.</summary>
    ExplicitConsent = 1,

    /// <summary>Art. 11, II, a - compliance with a legal or regulatory obligation.</summary>
    LegalObligation = 2,

    /// <summary>Art. 11, II, b - shared processing for public policy execution.</summary>
    PublicPolicy = 3,

    /// <summary>Art. 11, II, c - studies by a research body with anonymization whenever possible.</summary>
    Research = 4,

    /// <summary>Art. 11, II, d - exercise of rights.</summary>
    ExerciseOfRights = 5,

    /// <summary>Art. 11, II, e - protection of life or physical safety.</summary>
    ProtectionOfLife = 6,

    /// <summary>Art. 11, II, f - health protection by professionals or health authorities.</summary>
    HealthProtection = 7,

    /// <summary>Art. 11, II, g - fraud prevention and data subject security safeguards.</summary>
    FraudPrevention = 8
}

