namespace LGPD.NET.Core.Exceptions;

/// <summary>
/// Exception thrown when a retention period has expired.
/// </summary>
public sealed class RetentionExpiredException : Exception
{
    /// <summary>Entity whose retention period expired.</summary>
    public string Entity { get; }

    /// <summary>Field whose retention period expired.</summary>
    public string Field { get; }

    /// <summary>Timestamp when the retention period expired.</summary>
    public DateTimeOffset ExpiredAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetentionExpiredException" /> class.
    /// </summary>
    /// <param name="entity">Entity name.</param>
    /// <param name="field">Field name.</param>
    /// <param name="expiredAt">Expiration timestamp.</param>
    public RetentionExpiredException(string entity, string field, DateTimeOffset expiredAt)
        : base($"Retention period for field '{field}' on entity '{entity}' expired on {expiredAt:O}.")
    {
        Entity = entity;
        Field = field;
        ExpiredAt = expiredAt;
    }
}
