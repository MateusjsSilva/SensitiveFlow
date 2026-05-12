using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Serializes audit outbox payloads without prescribing the outbox storage backend.
/// </summary>
public interface IAuditOutboxSerializer
{
    /// <summary>Serializes an audit record.</summary>
    string Serialize(AuditRecord record);

    /// <summary>Deserializes an audit record.</summary>
    AuditRecord Deserialize(string payload);
}

