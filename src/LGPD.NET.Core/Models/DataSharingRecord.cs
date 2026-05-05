using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

/// <summary>
/// Record describing a third-party sharing relationship for a processing operation.
/// </summary>
public sealed record DataSharingRecord
{
    /// <summary>Recipient or third party that receives the data.</summary>
    public required string Recipient { get; init; }

    /// <summary>Purpose of the sharing.</summary>
    public string Purpose { get; init; } = string.Empty;

    /// <summary>Destination country, when the sharing is international.</summary>
    public TransferCountry? Country { get; init; }

    /// <summary>Transfer safeguard mechanism, when applicable.</summary>
    public SafeguardMechanism? Mechanism { get; init; }
}
