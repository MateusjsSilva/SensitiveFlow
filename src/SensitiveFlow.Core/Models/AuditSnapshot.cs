using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Models;

/// <summary>
/// Immutable snapshot of an aggregate's annotated fields before and after a single change.
/// Use this when you need a per-aggregate audit trail (one row per change) instead of the
/// per-field <see cref="AuditRecord"/> trail (one row per modified property).
/// </summary>
/// <remarks>
/// <para>
/// <b>When to prefer snapshots over records:</b> aggregates whose fields are only meaningful
/// together (e.g. an Address with street/city/zip), or domains where reviewers expect to see
/// "what did this look like before vs. after" instead of a list of field-level diffs.
/// </para>
/// <para>
/// <see cref="BeforeJson"/> and <see cref="AfterJson"/> hold serialized representations of
/// the annotated state. The serialization format is up to the caller, but
/// <c>SensitiveFlow.Json</c> can produce already-redacted payloads when the snapshot is
/// going somewhere with weaker access controls than the primary store.
/// </para>
/// </remarks>
public sealed record AuditSnapshot
{
    /// <summary>Unique identifier of this snapshot.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Identifier of the data subject the aggregate belongs to.</summary>
    public required string DataSubjectId { get; init; }

    /// <summary>Aggregate type (typically the CLR type name).</summary>
    public required string Aggregate { get; init; }

    /// <summary>Aggregate identifier (the primary key, as a string).</summary>
    public required string AggregateId { get; init; }

    /// <summary>Operation performed on the aggregate.</summary>
    public AuditOperation Operation { get; init; } = AuditOperation.Update;

    /// <summary>Timestamp of the change.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Identifier of the actor that performed the change, when known.</summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// Pseudonymized IP token of the actor — same rules as <see cref="AuditRecord.IpAddressToken"/>.
    /// Never store the raw IP address.
    /// </summary>
    public string? IpAddressToken { get; init; }

    /// <summary>
    /// Serialized state of the annotated fields BEFORE the change. <c>null</c> for create operations.
    /// </summary>
    public string? BeforeJson { get; init; }

    /// <summary>
    /// Serialized state of the annotated fields AFTER the change. <c>null</c> for delete operations.
    /// </summary>
    public string? AfterJson { get; init; }
}
