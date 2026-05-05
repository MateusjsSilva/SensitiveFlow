using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Attributes;

/// <summary>
/// Defines the retention period and the action on expiration under Art. 15 and 16 of the LGPD.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RetentionDataAttribute : Attribute
{
    /// <summary>Retention period in years.</summary>
    public int Years { get; set; }

    /// <summary>Retention period in months (added to Years).</summary>
    public int Months { get; set; }

    /// <summary>Action executed when the period expires.</summary>
    public RetentionPolicy Policy { get; set; } = RetentionPolicy.AnonymizeOnExpiration;

    /// <summary>
    /// Calculates the expiration date from a given reference point using calendar-accurate
    /// <see cref="DateTimeOffset.AddYears"/> and <see cref="DateTimeOffset.AddMonths"/>.
    /// </summary>
    /// <param name="from">The reference date from which the retention period starts (e.g. the creation date of the record).</param>
    /// <returns>The date on which the retention period expires.</returns>
    /// <remarks>
    /// Prefer this method over a fixed <c>TimeSpan</c>, which cannot account for leap years
    /// or months of varying length — an error that can cause premature deletion or retention
    /// beyond the legally declared period.
    /// </remarks>
    public DateTimeOffset GetExpirationDate(DateTimeOffset from) =>
        from.AddYears(Years).AddMonths(Months);
}
