namespace LGPD.NET.Core.Exceptions;

public sealed class RetentionExpiredException : Exception
{
    public string Entity { get; }
    public string Field { get; }
    public DateTimeOffset ExpiredAt { get; }

    public RetentionExpiredException(string entity, string field, DateTimeOffset expiredAt)
        : base($"Retention period for field '{field}' on entity '{entity}' expired on {expiredAt:O}.")
    {
        Entity = entity;
        Field = field;
        ExpiredAt = expiredAt;
    }
}
