using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Defines the retention period and the action on expiration.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RetentionDataAttribute : Attribute
{
    private int _years;
    private int _months;

    /// <summary>Retention period in years. Must be zero or positive.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
    public int Years
    {
        get => _years;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Retention period (Years) must be zero or positive.");
            }
            _years = value;
        }
    }

    /// <summary>Retention period in months (added to Years). Must be zero or positive.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
    public int Months
    {
        get => _months;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Retention period (Months) must be zero or positive.");
            }
            _months = value;
        }
    }

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
        from.AddYears(_years).AddMonths(_months);
}


