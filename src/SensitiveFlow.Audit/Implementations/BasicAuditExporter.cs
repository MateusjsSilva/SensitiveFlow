using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Implementations;

/// <summary>
/// Basic audit exporter supporting CSV and JSON formats.
/// For Parquet support, use a specialized implementation with external dependencies.
/// </summary>
public sealed class BasicAuditExporter : IAuditExporter
{
    /// <inheritdoc />
    public async Task<string> ExportAsCsvAsync(
        IAsyncEnumerable<AuditRecord> records,
        bool includeHash = true,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        // Write header
        var headers = new List<string>
        {
            "Id", "DataSubjectId", "Entity", "Field", "Operation", "Timestamp",
            "ActorId", "IpAddressToken", "Details"
        };
        if (includeHash)
        {
            headers.Add("PreviousRecordHash");
            headers.Add("CurrentRecordHash");
        }

        sb.AppendLine(string.Join(",", headers.Select(CsvEscape)));

        // Write records
        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            var values = new List<string>
            {
                record.Id.ToString(),
                record.DataSubjectId,
                record.Entity,
                record.Field,
                record.Operation.ToString(),
                record.Timestamp.ToString("O"),
                record.ActorId ?? string.Empty,
                record.IpAddressToken ?? string.Empty,
                record.Details ?? string.Empty
            };
            if (includeHash)
            {
                values.Add(record.PreviousRecordHash ?? string.Empty);
                values.Add(record.CurrentRecordHash ?? string.Empty);
            }

            sb.AppendLine(string.Join(",", values.Select(CsvEscape)));
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<string> ExportAsJsonAsync(
        IAsyncEnumerable<AuditRecord> records,
        bool prettyPrint = true,
        bool includeHash = true,
        CancellationToken cancellationToken = default)
    {
        var recordList = new List<AuditRecord>();

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            recordList.Add(record);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = prettyPrint,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        if (!includeHash)
        {
            // Create clean records without hash fields
            var cleanRecords = recordList.Select(r => new
            {
                r.Id,
                r.DataSubjectId,
                r.Entity,
                r.Field,
                r.Operation,
                r.Timestamp,
                r.ActorId,
                r.IpAddressToken,
                r.Details
            }).ToList();

            return JsonSerializer.Serialize(cleanRecords, options);
        }

        return JsonSerializer.Serialize(recordList, options);
    }

    /// <inheritdoc />
    public Task ExportAsParquetAsync(
        IAsyncEnumerable<AuditRecord> records,
        string outputPath,
        bool includeHash = true,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Parquet export requires external dependency. Use ParquetSharp or Apache.Arrow packages.");
    }

    /// <inheritdoc />
    public IDictionary<string, object?> RecordToDictionary(
        AuditRecord record,
        bool includeHash = true)
    {
        var dict = new Dictionary<string, object?>
        {
            { "Id", record.Id },
            { "DataSubjectId", record.DataSubjectId },
            { "Entity", record.Entity },
            { "Field", record.Field },
            { "Operation", record.Operation.ToString() },
            { "Timestamp", record.Timestamp.ToString("O") },
            { "ActorId", record.ActorId },
            { "IpAddressToken", record.IpAddressToken },
            { "Details", record.Details }
        };

        if (includeHash)
        {
            dict["PreviousRecordHash"] = record.PreviousRecordHash;
            dict["CurrentRecordHash"] = record.CurrentRecordHash;
        }

        return dict;
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
