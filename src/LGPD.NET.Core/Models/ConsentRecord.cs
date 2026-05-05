using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

public sealed record ConsentRecord
{
    public required string DataSubjectId { get; init; }
    public required ProcessingPurpose Purpose { get; init; }
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public bool Revoked { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
}
