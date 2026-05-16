namespace SensitiveFlow.Retention.Scheduling;

/// <summary>
/// Represents a batch of entities to process in parallel retention execution.
/// </summary>
/// <remarks>
/// Properties:
/// - Entities: The entities to process
/// - ReferenceSelector: Selector function to extract the reference date from each entity
/// </remarks>
public sealed record RetentionBatch(
    IEnumerable<object> Entities,
    Func<object, DateTimeOffset> ReferenceSelector
);
