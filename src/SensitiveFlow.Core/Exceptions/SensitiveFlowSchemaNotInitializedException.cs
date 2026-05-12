namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Thrown when a SensitiveFlow store fails to access its persistence schema because
/// the underlying table is missing.
/// </summary>
/// <remarks>
/// This wraps the provider-specific exception (e.g. <c>SqliteException</c>,
/// <c>SqlException</c>, <c>NpgsqlException</c>) with an actionable message pointing the
/// caller at <c>EnsureCreatedAsync()</c> (for samples) or <c>dotnet ef database update</c>
/// (for production deployments).
/// </remarks>
public sealed class SensitiveFlowSchemaNotInitializedException : SensitiveFlowException
{
    /// <summary>Machine-readable error code.</summary>
    public const string ErrorCode = "SF-SCHEMA-001";

    /// <summary>Initializes a new instance.</summary>
    /// <param name="tableName">Name of the missing table (best-effort, may be null).</param>
    /// <param name="contextName">Type name of the EF Core <c>DbContext</c> that owns the table.</param>
    /// <param name="innerException">The original provider-specific exception.</param>
    public SensitiveFlowSchemaNotInitializedException(
        string? tableName,
        string contextName,
        Exception innerException)
        : base(BuildMessage(tableName, contextName), ErrorCode, innerException)
    {
        TableName = tableName;
        ContextName = contextName;
    }

    /// <summary>Name of the missing table, when detectable.</summary>
    public string? TableName { get; }

    /// <summary>The EF Core <c>DbContext</c> type that owns the missing table.</summary>
    public string ContextName { get; }

    private static string BuildMessage(string? tableName, string contextName)
    {
        var tableHint = string.IsNullOrWhiteSpace(tableName)
            ? "the required SensitiveFlow table"
            : $"table '{tableName}'";

        return $"SensitiveFlow could not access {tableHint} on '{contextName}': the table does not exist. " +
               "Create the schema before starting the app. For samples, call " +
               "'await db.Database.EnsureCreatedAsync()' on each SensitiveFlow context. " +
               "For production, run EF Core migrations ('dotnet ef database update') or apply the " +
               "SQL scripts shipped in 'tools/migrations/<provider>/'.";
    }
}
