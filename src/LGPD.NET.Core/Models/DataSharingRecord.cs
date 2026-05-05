using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

public sealed record DataSharingRecord
{
    public required string Recipient { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public TransferCountry? Country { get; init; }
    public SafeguardMechanism? Mechanism { get; init; }
}
