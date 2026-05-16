using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Exporter for audit records in various formats (CSV, JSON, Parquet).
/// </summary>
public interface IAuditExporter
{
    /// <summary>
    /// Exports audit records in CSV format.
    /// </summary>
    /// <param name="records">Audit records to export.</param>
    /// <param name="includeHash">Include integrity hash fields (PreviousRecordHash, CurrentRecordHash).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CSV content as string.</returns>
    /// <remarks>
    /// Output includes headers: Id, DataSubjectId, Entity, Field, Operation, Timestamp, ActorId, IpAddressToken, Details, [PreviousRecordHash, CurrentRecordHash if requested].
    /// </remarks>
    Task<string> ExportAsCsvAsync(
        IAsyncEnumerable<AuditRecord> records,
        bool includeHash = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports audit records as a JSON array.
    /// </summary>
    /// <param name="records">Audit records to export.</param>
    /// <param name="prettyPrint">Format output with indentation and line breaks.</param>
    /// <param name="includeHash">Include integrity hash fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON content as string.</returns>
    Task<string> ExportAsJsonAsync(
        IAsyncEnumerable<AuditRecord> records,
        bool prettyPrint = true,
        bool includeHash = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports audit records in Parquet format (columnar, compressed).
    /// </summary>
    /// <param name="records">Audit records to export.</param>
    /// <param name="outputPath">File path where Parquet file will be written.</param>
    /// <param name="includeHash">Include integrity hash fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Parquet format is ideal for large datasets (>1M records) and analytical queries.
    /// Requires additional NuGet dependency: ParquetSharp or similar.
    /// </remarks>
    Task ExportAsParquetAsync(
        IAsyncEnumerable<AuditRecord> records,
        string outputPath,
        bool includeHash = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a single audit record to a dictionary (for flexible formatting).
    /// </summary>
    /// <param name="record">Audit record to export.</param>
    /// <param name="includeHash">Include integrity hash fields.</param>
    /// <returns>Dictionary representation of the record.</returns>
    IDictionary<string, object?> RecordToDictionary(
        AuditRecord record,
        bool includeHash = true);
}
