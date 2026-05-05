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
    public RetentionPolicy Policy { get; set; } = RetentionPolicy.AnonymizeOnExpiry;

    public TimeSpan Period => TimeSpan.FromDays((Years * 365) + (Months * 30));
}
