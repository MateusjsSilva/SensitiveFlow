using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

public sealed record DataSubjectRequest
{
    public required string DataSubjectId { get; init; }
    public required DataSubjectRequestType Type { get; init; }
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? Notes { get; init; }
}
