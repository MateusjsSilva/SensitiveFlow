using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.EFCore.Entities;

/// <summary>
/// EF Core persistence shape for <see cref="AuditRecord"/>. Uses <see cref="AuditOperation"/>
/// stored as <see cref="int"/> so the schema is portable across providers without enum mapping.
/// </summary>
public sealed class AuditRecordEntity
{
    /// <summary>Surrogate key used by the database. Distinct from <see cref="RecordId"/>.</summary>
    public long Id { get; set; }

    /// <summary>Stable identifier of the audit record (the <see cref="AuditRecord.Id"/>).</summary>
    public string RecordId { get; set; } = string.Empty;

    /// <summary>Subject identifier the record refers to.</summary>
    public string DataSubjectId { get; set; } = string.Empty;

    /// <summary>Entity type name.</summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>Field name on the entity.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Operation as <see cref="int"/> (cast of <see cref="AuditOperation"/>).</summary>
    public int Operation { get; set; }

    /// <summary>UTC timestamp of the operation.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Actor identifier (when known).</summary>
    public string? ActorId { get; set; }

    /// <summary>Pseudonymized IP token (never raw).</summary>
    public string? IpAddressToken { get; set; }

    /// <summary>Free-form details.</summary>
    public string? Details { get; set; }

    /// <summary>Maps to a domain <see cref="AuditRecord"/>.</summary>
    public AuditRecord ToRecord() => new()
    {
        Id = Guid.TryParse(RecordId, out var parsed) ? parsed : Guid.Empty,
        DataSubjectId = DataSubjectId,
        Entity = Entity,
        Field = Field,
        Operation = (AuditOperation)Operation,
        Timestamp = Timestamp,
        ActorId = ActorId,
        IpAddressToken = IpAddressToken,
        Details = Details,
    };

    /// <summary>Builds an entity from a domain <see cref="AuditRecord"/>.</summary>
    public static AuditRecordEntity FromRecord(AuditRecord record) => new()
    {
        RecordId = record.Id.ToString(),
        DataSubjectId = record.DataSubjectId,
        Entity = record.Entity,
        Field = record.Field,
        Operation = (int)record.Operation,
        Timestamp = record.Timestamp,
        ActorId = record.ActorId,
        IpAddressToken = record.IpAddressToken,
        Details = record.Details,
    };
}
