namespace SensitiveFlow.Retention.Contracts;

/// <summary>
/// Handles the action to take when a retention period expires for a specific entity and field.
/// Implement this interface and register it in DI to plug in custom expiration logic.
/// </summary>
public interface IRetentionExpirationHandler
{
    /// <summary>
    /// Executes the expiration action for the given entity instance.
    /// </summary>
    /// <param name="entity">The entity whose retention period has expired.</param>
    /// <param name="fieldName">Name of the field whose retention period expired.</param>
    /// <param name="expiredAt">Timestamp when the period expired.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(object entity, string fieldName, DateTimeOffset expiredAt, CancellationToken cancellationToken = default);
}
