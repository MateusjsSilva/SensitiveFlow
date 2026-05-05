using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

/// <summary>
/// Evidence-bearing consent record for a data subject and processing purpose.
/// </summary>
public sealed record ConsentRecord
{
    /// <summary>Unique identifier of the consent record.</summary>
    public required string Id { get; init; }

    /// <summary>Identifier of the data subject that granted consent.</summary>
    public required string DataSubjectId { get; init; }

    /// <summary>Processing purpose covered by the consent.</summary>
    public required ProcessingPurpose Purpose { get; init; }

    /// <summary>Legal basis associated with the record.</summary>
    public LegalBasis LegalBasis { get; init; } = LegalBasis.Consent;

    /// <summary>Timestamp when consent was collected.</summary>
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Expiration timestamp, when consent is time-limited.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Evidence showing how consent was collected.</summary>
    public string Evidence { get; init; } = string.Empty;

    /// <summary>Channel through which consent was collected.</summary>
    public string CollectionChannel { get; init; } = string.Empty;

    /// <summary>Privacy policy version accepted by the data subject.</summary>
    public string PrivacyPolicyVersion { get; init; } = string.Empty;

    /// <summary>Whether consent has been revoked.</summary>
    public bool Revoked { get; init; }

    /// <summary>Timestamp when consent was revoked, when applicable.</summary>
    public DateTimeOffset? RevokedAt { get; init; }
}
