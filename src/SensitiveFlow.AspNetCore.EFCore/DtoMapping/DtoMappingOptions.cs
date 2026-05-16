using System.Collections.Concurrent;

namespace SensitiveFlow.AspNetCore.EFCore.DtoMapping;

/// <summary>
/// Configuration for automatic DTO mapping and non-sensitive DTO registration.
/// </summary>
public sealed class DtoMappingOptions
{
    private readonly ConcurrentDictionary<Type, Type> _mappings = new();

    /// <summary>
    /// Gets all registered entity-to-DTO mappings.
    /// </summary>
    public IReadOnlyDictionary<Type, Type> Mappings => _mappings;

    /// <summary>
    /// Registers a mapping from an entity type to its non-sensitive DTO type.
    /// </summary>
    public void MapEntity<TEntity, TDto>() where TEntity : class where TDto : class
    {
        _mappings.TryAdd(typeof(TEntity), typeof(TDto));
    }

    /// <summary>
    /// Registers a mapping from an entity type to its non-sensitive DTO type.
    /// </summary>
    public void MapEntity(Type entityType, Type dtoType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(dtoType);

        if (!entityType.IsClass || entityType.IsAbstract)
        {
            throw new ArgumentException($"Entity type must be a concrete class: {entityType.FullName}", nameof(entityType));
        }

        if (!dtoType.IsClass || dtoType.IsAbstract)
        {
            throw new ArgumentException($"DTO type must be a concrete class: {dtoType.FullName}", nameof(dtoType));
        }

        _mappings.TryAdd(entityType, dtoType);
    }

    /// <summary>
    /// Gets the registered DTO type for an entity type, if any.
    /// </summary>
    public Type? GetDtoType(Type entityType)
    {
        _mappings.TryGetValue(entityType, out var dtoType);
        return dtoType;
    }

    /// <summary>
    /// Clears all registered mappings.
    /// </summary>
    public void Clear()
    {
        _mappings.Clear();
    }
}
