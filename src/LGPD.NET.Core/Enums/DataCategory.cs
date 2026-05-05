namespace LGPD.NET.Core.Enums;

/// <summary>
/// Categories of regular personal data under Art. 5, I of the LGPD.
/// Use with <c>PersonalDataAttribute</c>.
/// </summary>
/// <remarks>
/// <b>Do not use this enum for sensitive data.</b> Health, biometric, genetic, racial/ethnic origin,
/// political opinion, religious belief, sexual orientation, and trade-union membership are sensitive
/// personal data under Art. 5, II of the LGPD and carry additional obligations (Art. 11).
/// Use <see cref="SensitiveDataCategory"/> with <c>SensitiveDataAttribute</c> for those categories.
/// </remarks>
public enum DataCategory
{
    /// <summary>Unspecified or custom data category.</summary>
    Other = 0,

    /// <summary>Identification data, such as name or tax ID.</summary>
    Identification,

    /// <summary>Contact data, such as email, phone, or address.</summary>
    Contact,

    /// <summary>Financial data, such as bank account or credit card numbers.</summary>
    Financial,

    /// <summary>Judicial or legal proceeding data.</summary>
    JudicialData,

    /// <summary>Behavioral or profiling data.</summary>
    Behavioral,

    /// <summary>Location data.</summary>
    Location,
}
