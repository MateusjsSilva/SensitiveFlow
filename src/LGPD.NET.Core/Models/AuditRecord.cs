using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Models;

/// <summary>
/// Immutable record of an operation involving personal data.
/// </summary>
public sealed record AuditRecord
{
    /// <summary>Identifier of the data subject whose data was involved.</summary>
    public required string DataSubjectId { get; init; }

    /// <summary>Entity or aggregate that contains the data.</summary>
    public required string Entity { get; init; }

    /// <summary>Field or property involved in the operation.</summary>
    public required string Field { get; init; }

    /// <summary>Operation performed on the data.</summary>
    public AuditOperation Operation { get; init; } = AuditOperation.Access;

    /// <summary>Timestamp of the operation.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Identifier of the actor that performed the operation, when known.</summary>
    public string? ActorId { get; init; }

    /// <summary>IP address associated with the operation, when available.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Additional audit details.</summary>
    public string? Details { get; init; }
}
