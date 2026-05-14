using System.Text.Json;
using System.Text.Json.Serialization;

namespace SensitiveFlow.Core.Models;

/// <summary>
/// Typed structure for <see cref="AuditRecord.Details"/> field, enabling strongly-typed
/// queries and analysis of audit details without string parsing.
/// </summary>
/// <remarks>
/// <para>
/// This record is JSON-serialized and stored in <c>AuditRecord.Details</c> as a string.
/// Use <see cref="Parse"/> or <see cref="TryParse"/> to deserialize.
/// </para>
/// <para>
/// <b>Backward Compatibility:</b> If Details is a legacy string (pre-typed format),
/// parsing returns null gracefully rather than throwing.
/// </para>
/// </remarks>
public sealed record AuditRecordDetails
{
    /// <summary>
    /// Previous value before the operation (for updates/overwrites).
    /// Null if not applicable or if redaction prevented storage.
    /// </summary>
    [JsonPropertyName("oldValue")]
    public string? OldValue { get; init; }

    /// <summary>
    /// New value after the operation (for creates/updates).
    /// Null if not applicable or if redaction prevented storage.
    /// </summary>
    [JsonPropertyName("newValue")]
    public string? NewValue { get; init; }

    /// <summary>
    /// Tag identifying bulk operations: "bulk.update", "bulk.delete", "bulk.create".
    /// Null for row-level operations via SaveChanges.
    /// </summary>
    [JsonPropertyName("bulkOperationTag")]
    public string? BulkOperationTag { get; init; }

    /// <summary>
    /// Reason code for the operation: "compliance.erasure", "retention.purge", "user.request", etc.
    /// Used to correlate operations with external systems (tickets, approvals, batch jobs).
    /// </summary>
    [JsonPropertyName("reasonCode")]
    public string? ReasonCode { get; init; }

    /// <summary>
    /// Custom metadata (arbitrary JSON object as string).
    /// Examples: {"batchId": "batch-123"}, {"triggeredBy": "scheduler"}.
    /// </summary>
    [JsonPropertyName("metadata")]
    public string? Metadata { get; init; }

    /// <summary>
    /// Redaction action that was applied: "None", "Mask", "Redact", "Pseudonymize", "Omit".
    /// Indicates how the value was protected in OldValue/NewValue.
    /// </summary>
    [JsonPropertyName("redactionAction")]
    public string? RedactionAction { get; init; }

    /// <summary>
    /// Parses JSON-serialized AuditRecordDetails from a string.
    /// Returns null if the string is null, empty, or not valid JSON (backward compat).
    /// </summary>
    public static AuditRecordDetails? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            // Try to parse as JSON object first (typed format)
            if (json.StartsWith("{", StringComparison.Ordinal))
            {
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                return JsonSerializer.Deserialize<AuditRecordDetails>(json, options);
            }

            // Fallback: legacy unstructured string (e.g., "Audit redaction action: Mask; value: m****@x.com.")
            // Return null to indicate it's not structured
            return null;
        }
        catch (JsonException)
        {
            // Malformed JSON or legacy format — return null gracefully
            return null;
        }
    }

    /// <summary>
    /// Tries to parse AuditRecordDetails, returning false if parsing fails.
    /// </summary>
    public static bool TryParse(string? json, out AuditRecordDetails? result)
    {
        try
        {
            result = Parse(json);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Serializes this record to JSON for storage in <see cref="AuditRecord.Details"/>.
    /// Uses System.Text.Json by default.
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(this, options);
    }

}
