using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Exceptions;

public sealed class InternationalTransferNotAllowedException : Exception
{
    public TransferCountry Country { get; }
    public SafeguardMechanism? Mechanism { get; }
    public string? Reason { get; }

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
