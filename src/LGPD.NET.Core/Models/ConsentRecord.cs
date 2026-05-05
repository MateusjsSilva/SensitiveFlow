using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

public sealed record ConsentRecord
{
    public required string Id { get; init; }
    public required string DataSubjectId { get; init; }
    public required ProcessingPurpose Purpose { get; init; }
    public LegalBasis LegalBasis { get; init; } = LegalBasis.Consent;
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public string CollectionChannel { get; init; } = string.Empty;
    public string PrivacyPolicyVersion { get; init; } = string.Empty;
    public bool Revoked { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
}
