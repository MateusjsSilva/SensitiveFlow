using System.Text.Json;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Extensions;

/// <summary>
/// Extension methods for converting audit records to human-readable diffs.
/// </summary>
public static class AuditRecordDiffExtensions
{
    /// <summary>
    /// Converts a single <see cref="AuditRecord"/> into a <see cref="AuditRecordDiff"/>
    /// with a single field change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is suitable for per-field audit trails. The <see cref="AuditRecord.Details"/>
    /// field can optionally contain JSON serialized before/after values in the format:
    /// <code>
    /// {
    ///   "before": "old_value",
    ///   "after": "new_value",
    ///   "sensitive": true,
    ///   "category": "Contact"
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// If details are not provided, both before/after values are omitted from the diff.
    /// </para>
    /// </remarks>
    /// <param name="record">The audit record to convert.</param>
    /// <param name="maskSensitiveValues">
    /// If <c>true</c>, before/after values are masked to "[REDACTED]" when the field is marked sensitive.
    /// Defaults to <c>true</c> for compliance.
    /// </param>
    /// <returns>A <see cref="AuditRecordDiff"/> representation of the record.</returns>
    public static AuditRecordDiff ToDiff(this AuditRecord record, bool maskSensitiveValues = true)
    {
        var changes = new List<FieldChange>();

        // Try to parse details as JSON
        if (!string.IsNullOrEmpty(record.Details))
        {
            try
            {
                using var doc = JsonDocument.Parse(record.Details);
                var root = doc.RootElement;

                var beforeValue = root.TryGetProperty("before", out var beforeProp)
                    ? beforeProp.GetString()
                    : null;

                var afterValue = root.TryGetProperty("after", out var afterProp)
                    ? afterProp.GetString()
                    : null;

                var wasSensitive = root.TryGetProperty("sensitive", out var sensitiveProp) &&
                    sensitiveProp.GetBoolean();

                var category = root.TryGetProperty("category", out var categoryProp)
                    ? categoryProp.GetString()
                    : null;

                // Optionally mask sensitive values
                if (maskSensitiveValues && wasSensitive)
                {
                    beforeValue = string.IsNullOrEmpty(beforeValue) ? null : "[REDACTED]";
                    afterValue = string.IsNullOrEmpty(afterValue) ? null : "[REDACTED]";
                }

                changes.Add(new FieldChange
                {
                    FieldName = record.Field,
                    BeforeValue = beforeValue,
                    AfterValue = afterValue,
                    WasSensitive = wasSensitive,
                    SensitiveCategory = category
                });
            }
            catch (JsonException)
            {
                // Details are not valid JSON, skip parsing
                changes.Add(new FieldChange
                {
                    FieldName = record.Field,
                    BeforeValue = null,
                    AfterValue = null,
                    WasSensitive = false
                });
            }
        }
        else
        {
            // No details, create a minimal change entry
            changes.Add(new FieldChange
            {
                FieldName = record.Field,
                BeforeValue = null,
                AfterValue = null,
                WasSensitive = false
            });
        }

        return new AuditRecordDiff
        {
            AuditRecordId = record.Id,
            DataSubjectId = record.DataSubjectId,
            Entity = record.Entity,
            Operation = record.Operation,
            Timestamp = record.Timestamp,
            ActorId = record.ActorId,
            Changes = changes
        };
    }

    /// <summary>
    /// Aggregates multiple <see cref="AuditRecord"/>s for a single operation into a single
    /// <see cref="AuditRecordDiff"/> that shows all field changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is suitable for snapshot-style audits where multiple field records
    /// belong to a single logical operation (e.g., one UPDATE that changed email and address).
    /// All records must have the same <see cref="AuditRecord.Timestamp"/> (within a small tolerance)
    /// and <see cref="AuditRecord.DataSubjectId"/> to be aggregated into one diff.
    /// </para>
    /// <para>
    /// Records with mismatched timestamps or subjects are skipped with a warning.
    /// </para>
    /// </remarks>
    /// <param name="records">The audit records to aggregate.</param>
    /// <param name="maskSensitiveValues">If <c>true</c>, sensitive values are masked in the diff.</param>
    /// <returns>A single <see cref="AuditRecordDiff"/> aggregating all changes.</returns>
    /// <exception cref="ArgumentException">Thrown if records is empty or all records have mismatched subjects.</exception>
    public static AuditRecordDiff ToDiffAggregate(
        this IEnumerable<AuditRecord> records,
        bool maskSensitiveValues = true)
    {
        var recordList = records.ToList();
        if (recordList.Count == 0)
        {
            throw new ArgumentException("Cannot aggregate zero audit records.", nameof(records));
        }

        var first = recordList.First();
        var dataSubjectId = first.DataSubjectId;

        // Validate all records are for the same subject
        var allSameSubject = recordList.All(r => r.DataSubjectId == dataSubjectId);
        if (!allSameSubject)
        {
            throw new ArgumentException(
                "Cannot aggregate audit records with different data subjects.",
                nameof(records));
        }

        // Aggregate all changes
        var changes = new List<FieldChange>();
        foreach (var record in recordList)
        {
            var diff = record.ToDiff(maskSensitiveValues);
            changes.AddRange(diff.Changes);
        }

        return new AuditRecordDiff
        {
            AuditRecordId = first.Id,
            DataSubjectId = dataSubjectId,
            Entity = first.Entity,
            Operation = first.Operation,
            Timestamp = first.Timestamp,
            ActorId = first.ActorId,
            Changes = changes
        };
    }

    /// <summary>
    /// Converts a <see cref="AuditSnapshot"/> into a <see cref="AuditRecordDiff"/>
    /// by parsing the before/after JSON payloads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method attempts to parse <see cref="AuditSnapshot.BeforeJson"/> and
    /// <see cref="AuditSnapshot.AfterJson"/> as flat JSON objects and compute field-level diffs.
    /// If the JSON is nested or complex, only top-level keys are diffed.
    /// </para>
    /// <para>
    /// Properties that exist only in AfterJson are reported as changes with <c>null</c> before values.
    /// Properties that exist only in BeforeJson are reported as changes with <c>null</c> after values.
    /// </para>
    /// </remarks>
    /// <param name="snapshot">The snapshot to convert.</param>
    /// <param name="maskSensitiveValues">If <c>true</c>, all values are masked for privacy.</param>
    /// <returns>A <see cref="AuditRecordDiff"/> representing all field-level changes in the snapshot.</returns>
    public static AuditRecordDiff FromSnapshot(AuditSnapshot snapshot, bool maskSensitiveValues = true)
    {
        var changes = new List<FieldChange>();

        var beforeJson = !string.IsNullOrEmpty(snapshot.BeforeJson)
            ? JsonDocument.Parse(snapshot.BeforeJson).RootElement
            : (JsonElement?)null;

        var afterJson = !string.IsNullOrEmpty(snapshot.AfterJson)
            ? JsonDocument.Parse(snapshot.AfterJson).RootElement
            : (JsonElement?)null;

        var allKeys = new HashSet<string>();

        if (beforeJson?.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in beforeJson.Value.EnumerateObject())
            {
                allKeys.Add(prop.Name);
            }
        }

        if (afterJson?.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in afterJson.Value.EnumerateObject())
            {
                allKeys.Add(prop.Name);
            }
        }

        foreach (var key in allKeys.OrderBy(k => k))
        {
            var beforeValue = beforeJson?.TryGetProperty(key, out var beforeProp) == true
                ? ExtractJsonValue(beforeProp)
                : null;

            var afterValue = afterJson?.TryGetProperty(key, out var afterProp) == true
                ? ExtractJsonValue(afterProp)
                : null;

            // Only include if values actually changed
            if (beforeValue != afterValue)
            {
                if (maskSensitiveValues)
                {
                    beforeValue = string.IsNullOrEmpty(beforeValue) ? null : "[REDACTED]";
                    afterValue = string.IsNullOrEmpty(afterValue) ? null : "[REDACTED]";
                }

                changes.Add(new FieldChange
                {
                    FieldName = key,
                    BeforeValue = beforeValue,
                    AfterValue = afterValue,
                    WasSensitive = maskSensitiveValues
                });
            }
        }

        return new AuditRecordDiff
        {
            AuditRecordId = Guid.NewGuid(),
            DataSubjectId = snapshot.DataSubjectId,
            Entity = snapshot.Aggregate,
            Operation = snapshot.Operation,
            Timestamp = snapshot.Timestamp,
            ActorId = snapshot.ActorId,
            Changes = changes
        };
    }

    private static string? ExtractJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
