using System.Text.Json;
using System.Text.Json.Serialization;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Outbox;

/// <summary>
/// JSON serializer for audit outbox payloads.
/// </summary>
public sealed class JsonAuditOutboxSerializer : IAuditOutboxSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <inheritdoc />
    public string Serialize(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return JsonSerializer.Serialize(record, Options);
    }

    /// <inheritdoc />
    public AuditRecord Deserialize(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        return JsonSerializer.Deserialize<AuditRecord>(payload, Options)
            ?? throw new InvalidOperationException("Audit outbox payload did not contain an audit record.");
    }
}
