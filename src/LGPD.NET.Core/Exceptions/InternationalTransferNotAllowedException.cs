using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Exceptions;

public sealed class InternationalTransferNotAllowedException : Exception
{
    public TransferCountry Country { get; }

    public InternationalTransferNotAllowedException(TransferCountry country)
        : base($"International transfer to '{country}' is not allowed: safeguard mechanism missing or invalid (Art. 33 of the LGPD).")
    {
        Country = country;
    }
}
