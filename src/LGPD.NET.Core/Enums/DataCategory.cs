namespace LGPD.NET.Core.Enums;

/// <summary>
/// Categories of personal data commonly mapped for LGPD compliance.
/// </summary>
public enum DataCategory
{
    /// <summary>Unspecified or custom data category.</summary>
    Other = 0,

    /// <summary>Identification data, such as name or tax ID.</summary>
    Identification,

    /// <summary>Contact data, such as email, phone, or address.</summary>
    Contact,

    /// <summary>Financial data.</summary>
    Financial,

    /// <summary>Health-related data.</summary>
    Health,

    /// <summary>Biometric data.</summary>
    Biometric,

    /// <summary>Genetic data.</summary>
    Genetic,

    /// <summary>Racial or ethnic origin data.</summary>
    Ethnicity,

    /// <summary>Political opinion data.</summary>
    PoliticalOpinion,

    /// <summary>Religious belief data.</summary>
    ReligiousBelief,

    /// <summary>Sexual orientation data.</summary>
    SexualOrientation,

    /// <summary>Judicial or legal proceeding data.</summary>
    JudicialData,

    /// <summary>Behavioral or profiling data.</summary>
    Behavioral,

    /// <summary>Location data.</summary>
    Location
}
