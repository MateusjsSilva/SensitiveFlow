using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

public sealed record DataSubjectRequest
{
    public required string Id { get; init; }
    public required string DataSubjectId { get; init; }
    public DataSubjectKind DataSubjectKind { get; init; } = DataSubjectKind.Adult;
    public required DataSubjectRequestType Type { get; init; }
    public DataSubjectRequestStatus Status { get; init; } = DataSubjectRequestStatus.Open;
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResponseDueAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Notes { get; init; }
}
