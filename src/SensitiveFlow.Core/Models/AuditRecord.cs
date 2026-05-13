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
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Identifier of the data subject whose data was involved.</summary>
    public required string DataSubjectId { get; init; }

    /// <summary>Entity or aggregate that contains the data.</summary>
    public required string Entity { get; init; }

    /// <summary>Field or property involved in the operation.</summary>
    public required string Field { get; init; }

    /// <summary>Operation performed on the data.</summary>
    /// <remarks>
    /// Defaults to <see cref="AuditOperation.Access"/> so that callers that only record read
    /// events can omit this property. Override it explicitly for write/delete/export events
    /// to keep the audit trail accurate.
    /// </remarks>
    public AuditOperation Operation { get; init; } = AuditOperation.Access;

    /// <summary>Timestamp of the operation.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Identifier of the actor that performed the operation, when known.</summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Pseudonymized token representing the IP address associated with the operation.
    /// <para>
    /// <b>Never store the raw IP address here.</b> An IP address can identify an individual when combined with
    /// other metadata, so it should be treated as personal data. Before assigning this field, pseudonymize the IP using
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

    /// <summary>
    /// SHA-256 hash of the previous audit record in the chain (for a specific data subject).
    /// Used to detect tampering or deletion of records. If this is the first record for a subject,
    /// this field is <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field enables the detection of audit trail tampering by implementing a hash-linked
    /// chain. When verifying audit integrity, callers can reconstruct the hash chain and detect:
    /// <list type="bullet">
    ///   <item><description>Deleted records (missing link in chain)</description></item>
    ///   <item><description>Modified records (hash mismatch with next record)</description></item>
    ///   <item><description>Out-of-order records (hash references misaligned)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The hash is computed from the <see cref="Id"/>, <see cref="DataSubjectId"/>,
    /// <see cref="Entity"/>, <see cref="Field"/>, <see cref="Operation"/>, and
    /// <see cref="Timestamp"/> of the previous record. See <c>AuditRecordIntegrityHelper</c>
    /// for the hashing algorithm.
    /// </para>
    /// </remarks>
    public string? PreviousRecordHash { get; init; }

    /// <summary>
    /// SHA-256 hash of this audit record's immutable fields.
    /// Used as a reference point for subsequent records and for integrity verification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This field is computed when the record is persisted and should remain immutable.
    /// When computing the hash, only immutable fields are included: <see cref="Id"/>,
    /// <see cref="DataSubjectId"/>, <see cref="Entity"/>, <see cref="Field"/>,
    /// <see cref="Operation"/>, and <see cref="Timestamp"/>. Mutable fields like
    /// <see cref="Details"/> are not included to allow logging systems to append
    /// enrichment data without breaking the chain.
    /// </para>
    /// <para>
    /// If <c>null</c>, the record has not yet been persisted or the audit store
    /// does not support hash computation.
    /// </para>
    /// </remarks>
    public string? CurrentRecordHash { get; init; }
}


