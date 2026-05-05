using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Marks a property whose data can be transferred internationally under applicable international transfer rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public sealed class InternationalTransferAttribute : Attribute
{
    /// <summary>Destination country for the transfer.</summary>
    public TransferCountry Country { get; set; } = TransferCountry.Other;

    /// <summary>Safeguard mechanism that authorizes or supports the transfer.</summary>
    public SafeguardMechanism Mechanism { get; set; } = SafeguardMechanism.ContractualClauses;

    /// <summary>Recipient name (company or external service).</summary>
    public string? Recipient { get; set; }
}


