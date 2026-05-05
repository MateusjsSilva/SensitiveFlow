namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Exception thrown when data cannot be found by entity and identifier.
/// </summary>
public sealed class DataNotFoundException : Exception
{
    /// <summary>Entity name used in the lookup.</summary>
    public string Entity { get; }

    /// <summary>Identifier used in the lookup.</summary>
    public string Id { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataNotFoundException" /> class.
    /// </summary>
    /// <param name="entity">Entity name.</param>
    /// <param name="id">Entity identifier.</param>
    public DataNotFoundException(string entity, string id)
        : base($"Data not found for entity '{entity}' with id '{id}'.")
    {
        Entity = entity;
        Id = id;
    }
}
