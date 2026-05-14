namespace SensitiveFlow.Core.Models;

/// <summary>
/// Fluent query builder for audit record filtering and pagination.
/// Enables filtering by entity type, operation, actor, and time range.
/// </summary>
/// <remarks>
/// <para>
/// This builder allows structured queries instead of fetching all records and filtering in-memory.
/// Implementations (EFCore, InMemory) translate to efficient database queries.
/// </para>
/// <para>
/// All filter methods return this builder for chaining. Pass to store.QueryAsync() to execute.
/// </para>
/// </remarks>
public sealed class AuditQuery
{
    /// <summary>
    /// Filter by entity type name (e.g., "User", "Order").
    /// </summary>
    public string? Entity { get; private set; }

    /// <summary>
    /// Filter by operation (Create, Update, Delete, Bulk).
    /// </summary>
    public string? Operation { get; private set; }

    /// <summary>
    /// Filter by actor ID/user who performed the operation.
    /// </summary>
    public string? ActorId { get; private set; }

    /// <summary>
    /// Filter by data subject (who the record is about).
    /// </summary>
    public string? DataSubjectId { get; private set; }

    /// <summary>
    /// Filter by field/column name.
    /// </summary>
    public string? Field { get; private set; }

    /// <summary>
    /// Inclusive start timestamp for time range.
    /// </summary>
    public DateTimeOffset? From { get; private set; }

    /// <summary>
    /// Inclusive end timestamp for time range.
    /// </summary>
    public DateTimeOffset? To { get; private set; }

    /// <summary>
    /// Number of records to skip (for pagination).
    /// </summary>
    public int Skip { get; private set; } = 0;

    /// <summary>
    /// Maximum number of records to return (for pagination).
    /// </summary>
    public int Take { get; private set; } = 100;

    /// <summary>
    /// Property to sort by (default: Timestamp). Use "Timestamp", "DataSubjectId", "Entity", "Field", "Operation".
    /// </summary>
    public string OrderBy { get; private set; } = "Timestamp";

    /// <summary>
    /// Sort direction: true = descending, false = ascending.
    /// </summary>
    public bool OrderByDescending { get; private set; } = true;

    /// <summary>
    /// Filter by entity type name.
    /// </summary>
    public AuditQuery ByEntity(string entity)
    {
        Entity = entity;
        return this;
    }

    /// <summary>
    /// Filter by operation (Create, Update, Delete, Bulk).
    /// </summary>
    public AuditQuery ByOperation(string operation)
    {
        Operation = operation;
        return this;
    }

    /// <summary>
    /// Filter by actor ID who performed the operation.
    /// </summary>
    public AuditQuery ByActorId(string actorId)
    {
        ActorId = actorId;
        return this;
    }

    /// <summary>
    /// Filter by data subject (who the record is about).
    /// </summary>
    public AuditQuery ByDataSubject(string dataSubjectId)
    {
        DataSubjectId = dataSubjectId;
        return this;
    }

    /// <summary>
    /// Filter by field/column name.
    /// </summary>
    public AuditQuery ByField(string field)
    {
        Field = field;
        return this;
    }

    /// <summary>
    /// Set time range (inclusive on both ends).
    /// </summary>
    public AuditQuery InTimeRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        From = from;
        To = to;
        return this;
    }

    /// <summary>
    /// Set pagination (skip and take).
    /// </summary>
    public AuditQuery WithPagination(int skip, int take)
    {
        Skip = skip;
        Take = take;
        return this;
    }

    /// <summary>
    /// Set ordering by property and direction.
    /// </summary>
    public AuditQuery OrderByProperty(string propertyName, bool descending = true)
    {
        OrderBy = propertyName;
        OrderByDescending = descending;
        return this;
    }

    /// <summary>
    /// Returns a copy of this query for use in different contexts.
    /// </summary>
    public AuditQuery Clone()
    {
        return new AuditQuery
        {
            Entity = Entity,
            Operation = Operation,
            ActorId = ActorId,
            DataSubjectId = DataSubjectId,
            Field = Field,
            From = From,
            To = To,
            Skip = Skip,
            Take = Take,
            OrderBy = OrderBy,
            OrderByDescending = OrderByDescending
        };
    }
}
