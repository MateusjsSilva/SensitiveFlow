using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Snapshots.EFCore.Entities;

/// <summary>
/// EF Core persistence shape for <see cref="AuditSnapshot"/>.
/// </summary>
public sealed class AuditSnapshotEntity
{
    /// <summary>Surrogate key used by the database.</summary>
    public long Id { get; set; }

    /// <summary>Stable identifier of the snapshot (the <see cref="AuditSnapshot.Id"/>).</summary>
    public string SnapshotId { get; set; } = string.Empty;

    /// <summary>Subject identifier the snapshot refers to.</summary>
    public string DataSubjectId { get; set; } = string.Empty;

    /// <summary>Aggregate type name.</summary>
    public string Aggregate { get; set; } = string.Empty;

    /// <summary>Aggregate identifier (primary key as string).</summary>
    public string AggregateId { get; set; } = string.Empty;

    /// <summary>Operation as <see cref="int"/> (cast of <see cref="AuditOperation"/>).</summary>
    public int Operation { get; set; }

    /// <summary>UTC timestamp of the change.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Actor identifier (when known).</summary>
    public string? ActorId { get; set; }

    /// <summary>Pseudonymized IP token (never raw).</summary>
    public string? IpAddressToken { get; set; }

    /// <summary>Serialized state BEFORE the change.</summary>
    public string? BeforeJson { get; set; }

    /// <summary>Serialized state AFTER the change.</summary>
    public string? AfterJson { get; set; }

    /// <summary>Maps to a domain <see cref="AuditSnapshot"/>.</summary>
    public AuditSnapshot ToSnapshot() => new()
    {
        Id = Guid.TryParse(SnapshotId, out var parsed) ? parsed : Guid.Empty,
        DataSubjectId = DataSubjectId,
        Aggregate = Aggregate,
        AggregateId = AggregateId,
        Operation = (AuditOperation)Operation,
        Timestamp = Timestamp,
        ActorId = ActorId,
        IpAddressToken = IpAddressToken,
        BeforeJson = BeforeJson,
        AfterJson = AfterJson,
    };

    /// <summary>Builds an entity from a domain <see cref="AuditSnapshot"/>.</summary>
    public static AuditSnapshotEntity FromSnapshot(AuditSnapshot snapshot) => new()
    {
        SnapshotId = snapshot.Id.ToString(),
        DataSubjectId = snapshot.DataSubjectId,
        Aggregate = snapshot.Aggregate,
        AggregateId = snapshot.AggregateId,
        Operation = (int)snapshot.Operation,
        Timestamp = snapshot.Timestamp,
        ActorId = snapshot.ActorId,
        IpAddressToken = snapshot.IpAddressToken,
        BeforeJson = snapshot.BeforeJson,
        AfterJson = snapshot.AfterJson,
    };
}
