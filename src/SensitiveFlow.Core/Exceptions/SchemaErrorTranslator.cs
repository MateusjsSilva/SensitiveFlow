namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Translates provider-specific "table does not exist" exceptions into a friendly
/// <see cref="SensitiveFlowSchemaNotInitializedException"/> with remediation guidance.
/// </summary>
/// <remarks>
/// Cross-provider detection is best-effort and matches well-known message fragments:
/// <list type="bullet">
///   <item>SQLite: <c>no such table</c></item>
///   <item>SQL Server: <c>Invalid object name</c></item>
///   <item>PostgreSQL: <c>relation</c> + <c>does not exist</c></item>
///   <item>MySQL/MariaDB: <c>doesn't exist</c></item>
///   <item>Oracle: <c>ORA-00942</c></item>
/// </list>
/// If no match is found, the original exception is returned unchanged.
/// </remarks>
public static class SchemaErrorTranslator
{
    /// <summary>
    /// Returns a <see cref="SensitiveFlowSchemaNotInitializedException"/> wrapping
    /// <paramref name="exception"/> when the underlying message indicates the table
    /// does not exist. Otherwise returns <paramref name="exception"/> unchanged so the
    /// caller can <c>throw;</c> the original.
    /// </summary>
    public static Exception Translate(Exception exception, string contextName)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (TryGetMissingTable(exception, out var tableName))
        {
            return new SensitiveFlowSchemaNotInitializedException(tableName, contextName, exception);
        }

        return exception;
    }

    private static bool TryGetMissingTable(Exception exception, out string? tableName)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var msg = current.Message;
            if (string.IsNullOrEmpty(msg))
            {
                continue;
            }

            // SQLite: "SQLite Error 1: 'no such table: SensitiveFlow_TokenMappings'."
            var idx = msg.IndexOf("no such table:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                tableName = ExtractAfter(msg, idx + "no such table:".Length);
                return true;
            }

            // SQL Server: "Invalid object name 'SensitiveFlow_TokenMappings'."
            idx = msg.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                tableName = ExtractQuoted(msg, idx);
                return true;
            }

            // PostgreSQL: "relation \"sensitiveflow_tokenmappings\" does not exist"
            if (msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("relation", StringComparison.OrdinalIgnoreCase))
            {
                tableName = ExtractQuoted(msg, 0);
                return true;
            }

            // MySQL/MariaDB: "Table 'app.sensitiveflow_tokenmappings' doesn't exist"
            if (msg.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("Table", StringComparison.OrdinalIgnoreCase))
            {
                tableName = ExtractQuoted(msg, 0);
                return true;
            }

            // Oracle: "ORA-00942: table or view does not exist"
            if (msg.Contains("ORA-00942", StringComparison.OrdinalIgnoreCase))
            {
                tableName = null;
                return true;
            }
        }

        tableName = null;
        return false;
    }

    private static string? ExtractAfter(string message, int start)
    {
        var slice = message[start..].TrimStart();
        var end = slice.IndexOfAny(new[] { '\'', '"', '.', '\n', '\r' });
        return end > 0 ? slice[..end].Trim() : slice.Trim();
    }

    private static string? ExtractQuoted(string message, int start)
    {
        var slice = message[start..];
        var openSingle = slice.IndexOf('\'');
        var openDouble = slice.IndexOf('"');
        int open;
        char quote;
        if (openSingle == -1 && openDouble == -1)
        {
            return null;
        }
        if (openSingle == -1)
        {
            open = openDouble;
            quote = '"';
        }
        else if (openDouble == -1 || openSingle < openDouble)
        {
            open = openSingle;
            quote = '\'';
        }
        else
        {
            open = openDouble;
            quote = '"';
        }

        var close = slice.IndexOf(quote, open + 1);
        return close > open ? slice[(open + 1)..close] : null;
    }
}
