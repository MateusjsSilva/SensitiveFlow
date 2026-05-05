using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Models;

/// <summary>
/// Immutable record of an operation involving personal data.
/// </summary>
public sealed record AuditRecord
{
    /// <summary>
    /// Unique identifier of this audit record.
    /// Used to correlate events across systems and to implement idempotent appends.
    /// Defaults to a new <see cref="Guid"/> when not provided.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

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

    /// <summary>
    /// Pseudonymized token representing the IP address associated with the operation.
    /// <para>
    /// <b>Never store the raw IP address here.</b> An IP address is personal data under Art. 5, I
    /// of the LGPD and GDPR Recital 49. Before assigning this field, pseudonymize the IP using
    /// <c>TokenPseudonymizer</c> backed by a durable store so that it can be resolved during
    /// a security investigation while remaining opaque in the audit log itself.
    /// </para>
    /// <example>
    /// <code>
    /// var ipToken = await pseudonymizer.PseudonymizeAsync(request.HttpContext.Connection.RemoteIpAddress?.ToString());
    /// var record  = new AuditRecord { ..., IpAddressToken = ipToken };
    /// </code>
    /// </example>
    /// </summary>
    public string? IpAddressToken { get; init; }

    /// <summary>Additional audit details.</summary>
    public string? Details { get; init; }
}
