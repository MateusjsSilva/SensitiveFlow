using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Exceptions;

/// <summary>
/// Exception thrown when an international transfer is not allowed.
/// </summary>
public sealed class InternationalTransferNotAllowedException : Exception
{
    /// <summary>Destination country for the transfer.</summary>
    public TransferCountry Country { get; }

    /// <summary>Safeguard mechanism evaluated for the transfer, when available.</summary>
    public SafeguardMechanism? Mechanism { get; }

    /// <summary>Reason why the transfer was rejected.</summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternationalTransferNotAllowedException" /> class.
    /// </summary>
    /// <param name="country">Destination country.</param>
    /// <param name="mechanism">Safeguard mechanism, when available.</param>
    /// <param name="reason">Reason why the transfer was rejected.</param>
    public InternationalTransferNotAllowedException(
        TransferCountry country,
        SafeguardMechanism? mechanism = null,
        string? reason = null)
        : base($"International transfer to '{country}' is not allowed: {reason ?? "safeguard mechanism missing or invalid"} (Art. 33 of the LGPD).")
    {
        Country = country;
        Mechanism = mechanism;
        Reason = reason;
    }
}
