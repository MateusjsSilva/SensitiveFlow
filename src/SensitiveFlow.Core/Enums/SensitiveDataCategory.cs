namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Categories of sensitive personal data.
/// These categories carry additional processing obligations and restricted legal bases.
/// Use <c>SensitiveDataAttribute</c> — not <c>PersonalDataAttribute</c> — to annotate
/// properties that fall into these categories.
/// </summary>
public enum SensitiveDataCategory
{
    /// <summary>Unspecified or custom sensitive data category.</summary>
    Other = 0,

    /// <summary>Financial data, such as salary, bank account, or credit card numbers.</summary>
    Financial,

    /// <summary>Health or medical data </summary>
    Health,

    /// <summary>Biometric data used for identification </summary>
    Biometric,

    /// <summary>Genetic data </summary>
    Genetic,

    /// <summary>Racial or ethnic origin </summary>
    Ethnicity,

    /// <summary>Political opinion </summary>
    PoliticalOpinion,

    /// <summary>Religious or philosophical belief </summary>
    ReligiousBelief,

    /// <summary>Sexual orientation </summary>
    SexualOrientation,

    /// <summary>Trade union membership </summary>
    TradeUnionMembership,

    /// <summary>Criminal records or proceedings </summary>
    Criminal,
}


