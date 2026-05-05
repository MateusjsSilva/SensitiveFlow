namespace LGPD.NET.Core.Models;

public sealed record AuditRecord
{
    public required string DataSubjectId { get; init; }
    public required string Entity { get; init; }
    public required string Field { get; init; }
    public required string Operation { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? ActorId { get; init; }
    public string? IpAddress { get; init; }
    public string? Details { get; init; }
}
