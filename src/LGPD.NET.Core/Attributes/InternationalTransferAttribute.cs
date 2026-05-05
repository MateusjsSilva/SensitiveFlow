using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Attributes;

/// <summary>
/// Marks a property whose data can be transferred internationally under Art. 33-36 of the LGPD.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public sealed class InternationalTransferAttribute : Attribute
{
    public TransferCountry Country { get; set; } = TransferCountry.Other;
    public SafeguardMechanism Safeguard { get; set; } = SafeguardMechanism.StandardContractualClauses;

    /// <summary>Recipient name (company or external service).</summary>
    public string? Recipient { get; set; }
}
