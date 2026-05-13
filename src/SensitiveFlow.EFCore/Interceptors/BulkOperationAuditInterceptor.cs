using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.EFCore.Interceptors;

/// <summary>
/// Command interceptor that audits bulk update and delete operations (ExecuteUpdateAsync, ExecuteDeleteAsync).
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="SensitiveDataAuditInterceptor"/>, which intercepts SaveChanges and tracks
/// per-entity changes, this interceptor detects ExecuteUpdate/ExecuteDelete commands at the
/// SQL level and records them as bulk operations.
/// </para>
/// <para>
/// <b>Limitations:</b>
/// <list type="bullet">
///   <item><description>Only logs that a bulk operation occurred, not individual entity details</description></item>
///   <item><description>Before/after values are not captured (requires separate queries)</description></item>
///   <item><description>DataSubjectId must be inferred from the WHERE clause (implementation-dependent)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Behavior:</b> When an ExecuteUpdate or ExecuteDelete command is detected, an audit record
/// is created with operation type set to "BulkUpdate" or "BulkDelete", and the SQL command
/// text is logged in the Details field.
/// </para>
/// </remarks>
public sealed class BulkOperationAuditInterceptor
{
    private readonly IAuditStore _auditStore;
    private readonly IAuditContext _auditContext;

    /// <summary>
    /// Initializes a new instance of <see cref="BulkOperationAuditInterceptor"/>.
    /// </summary>
    public BulkOperationAuditInterceptor(IAuditStore auditStore, IAuditContext auditContext)
    {
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _auditContext = auditContext ?? throw new ArgumentNullException(nameof(auditContext));
    }

    /// <summary>
    /// NOT IMPLEMENTED: Reserved for future bulk operation tracking functionality.
    /// Currently a placeholder pending proper interceptor API integration with EF Core 9.0+.
    /// </summary>
    [Obsolete("This interceptor is not yet implemented for EF Core 9.0+", true)]
    public ValueTask<DbCommand> CommandCreatedAsync(
        object eventData,
        DbCommand result,
        CancellationToken cancellationToken = default)
    {
        // Check if this is an ExecuteUpdate or ExecuteDelete command
        if (eventData.Command?.CommandText is not null)
        {
            await DetectAndAuditBulkOperationAsync(eventData, result, cancellationToken);
        }

        throw new NotImplementedException("BulkOperationAuditInterceptor is not yet implemented for EF Core 9.0+");
    }

    private async Task DetectAndAuditBulkOperationAsync(
        CommandEndEventData eventData,
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var commandText = command.CommandText;
        if (string.IsNullOrEmpty(commandText))
            return;

        // Detect bulk UPDATE
        if (commandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) &&
            commandText.Contains("SET", StringComparison.OrdinalIgnoreCase))
        {
            await AuditBulkUpdateAsync(commandText, cancellationToken);
        }
        // Detect bulk DELETE
        else if (commandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase) &&
                 commandText.Contains("FROM", StringComparison.OrdinalIgnoreCase))
        {
            await AuditBulkDeleteAsync(commandText, cancellationToken);
        }
    }

    private async Task AuditBulkUpdateAsync(string commandText, CancellationToken cancellationToken)
    {
        // Extract table and column names from SQL
        // This is a basic implementation; production code might use more sophisticated SQL parsing
        var tableName = ExtractTableName(commandText);
        var updatedColumns = ExtractUpdatedColumns(commandText);

        var record = new AuditRecord
        {
            DataSubjectId = "BULK_OPERATION",  // Placeholder; actual subject unknown without query execution
            Entity = tableName ?? "Unknown",
            Field = string.Join(", ", updatedColumns),
            Operation = SensitiveFlow.Core.Enums.AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
            Details = JsonSerializer.Serialize(new
            {
                operation = "ExecuteUpdate",
                command = commandText,
                timestamp = DateTimeOffset.UtcNow
            })
        };

        await _auditStore.AppendAsync(record, cancellationToken);
    }

    private async Task AuditBulkDeleteAsync(string commandText, CancellationToken cancellationToken)
    {
        var tableName = ExtractTableName(commandText);

        var record = new AuditRecord
        {
            DataSubjectId = "BULK_OPERATION",  // Placeholder; actual subject unknown without query execution
            Entity = tableName ?? "Unknown",
            Field = "*",  // Indicates entire rows deleted
            Operation = SensitiveFlow.Core.Enums.AuditOperation.Delete,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
            Details = JsonSerializer.Serialize(new
            {
                operation = "ExecuteDelete",
                command = commandText,
                timestamp = DateTimeOffset.UtcNow
            })
        };

        await _auditStore.AppendAsync(record, cancellationToken);
    }

    private static string? ExtractTableName(string commandText)
    {
        // Very basic extraction; for production, use a proper SQL parser
        var updateMatch = System.Text.RegularExpressions.Regex.Match(
            commandText,
            @"UPDATE\s+(?:\[?(\w+)\]?)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (updateMatch.Success)
            return updateMatch.Groups[1].Value;

        var deleteMatch = System.Text.RegularExpressions.Regex.Match(
            commandText,
            @"FROM\s+(?:\[?(\w+)\]?)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (deleteMatch.Success)
            return deleteMatch.Groups[1].Value;

        return null;
    }

    private static List<string> ExtractUpdatedColumns(string commandText)
    {
        var columns = new List<string>();
        var setMatch = System.Text.RegularExpressions.Regex.Match(
            commandText,
            @"SET\s+(.+?)(?=WHERE|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (setMatch.Success)
        {
            var setClause = setMatch.Groups[1].Value;
            var columnMatches = System.Text.RegularExpressions.Regex.Matches(
                setClause,
                @"(\w+)\s*=");

            foreach (System.Text.RegularExpressions.Match match in columnMatches)
            {
                columns.Add(match.Groups[1].Value);
            }
        }

        return columns;
    }
}
