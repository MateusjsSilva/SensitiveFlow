using System.Reflection;

namespace SensitiveFlow.AspNetCore.EFCore.DtoMapping;

/// <summary>
/// Maps entity instances to their registered non-sensitive DTO equivalents.
/// </summary>
public sealed class DtoMapper
{
    private readonly DtoMappingOptions _options;

    /// <summary>Initializes a new instance of <see cref="DtoMapper"/>.</summary>
    public DtoMapper(DtoMappingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Maps an entity to its registered DTO type if a mapping exists; otherwise returns the entity unchanged.
    /// </summary>
    public object Map(object? entity)
    {
        if (entity is null)
        {
            return null!;
        }

        var entityType = entity.GetType();
        var dtoType = _options.GetDtoType(entityType);

        if (dtoType is null)
        {
            return entity;
        }

        return MapToDto(entity, entityType, dtoType);
    }

    /// <summary>
    /// Maps an enumerable of entities to their registered DTO types.
    /// </summary>
    public IEnumerable<object> MapEnumerable(IEnumerable<object>? entities)
    {
        if (entities is null)
        {
            return Enumerable.Empty<object>();
        }

        return entities.Select(Map);
    }

    private object MapToDto(object entity, Type entityType, Type dtoType)
    {
        var dtoInstance = Activator.CreateInstance(dtoType)
            ?? throw new InvalidOperationException($"Failed to instantiate DTO type: {dtoType.FullName}");

        var entityProperties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var dtoProperties = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var entityProp in entityProperties)
        {
            if (!entityProp.CanRead)
            {
                continue;
            }

            var dtoProp = dtoProperties.FirstOrDefault(p => p.Name == entityProp.Name && p.CanWrite);
            if (dtoProp is null)
            {
                continue;
            }

            try
            {
                var value = entityProp.GetValue(entity);
                dtoProp.SetValue(dtoInstance, value);
            }
            catch
            {
                // Skip properties that cannot be mapped
            }
        }

        return dtoInstance;
    }
}
