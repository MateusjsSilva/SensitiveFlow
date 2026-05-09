namespace SensitiveFlow.Audit.EFCore.Maintenance;

/// <summary>
/// Allows purging audit records that have exceeded their retention horizon.
/// Inject this interface to schedule or trigger log cleanup without depending
/// on the concrete <see cref="AuditLogRetention{TContext}"/> implementation.
/// </summary>
public interface IAuditLogRetention
{
    /// <summary>
    /// Deletes audit records whose timestamp is older than <paramref name="olderThan"/>.
    /// Returns the number of rows deleted.
    /// </summary>
    Task<int> PurgeOlderThanAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
