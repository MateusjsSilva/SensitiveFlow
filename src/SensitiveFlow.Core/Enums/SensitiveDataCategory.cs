namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Categories of sensitive personal data under Art. 5, II of the LGPD.
/// These categories carry additional processing obligations and restricted legal bases (Art. 11).
/// Use <c>SensitiveDataAttribute</c> — not <c>PersonalDataAttribute</c> — to annotate
/// properties that fall into these categories.
/// </summary>
public enum SensitiveDataCategory
{
    /// <summary>Unspecified or custom sensitive data category.</summary>
    Other = 0,

    /// <summary>Health or medical data (Art. 5, II).</summary>
    Health,

    /// <summary>Biometric data used for identification (Art. 5, II).</summary>
    Biometric,

    /// <summary>Genetic data (Art. 5, II).</summary>
    Genetic,

    /// <summary>Racial or ethnic origin (Art. 5, II).</summary>
    Ethnicity,

    /// <summary>Political opinion (Art. 5, II).</summary>
    PoliticalOpinion,

    /// <summary>Religious or philosophical belief (Art. 5, II).</summary>
    ReligiousBelief,

    /// <summary>Sexual orientation (Art. 5, II).</summary>
    SexualOrientation,

    /// <summary>Trade union membership (Art. 5, II).</summary>
    TradeUnionMembership,
}

