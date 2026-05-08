using System.Reflection;

namespace SensitiveFlow.Anonymization.Erasure;

/// <summary>
/// Default erasure strategy that overwrites string properties with a fixed marker
/// (default <c>"[ERASED]"</c>) and sets non-string properties to their type default.
/// </summary>
public sealed class RedactionErasureStrategy : IErasureStrategy
{
    private readonly string _marker;

    /// <summary>Initializes a new instance with the default marker.</summary>
    public RedactionErasureStrategy() : this("[ERASED]") { }

    /// <summary>Initializes a new instance with a custom marker.</summary>
    public RedactionErasureStrategy(string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        _marker = marker;
    }

    /// <inheritdoc />
    public object? GetErasureValue(object entity, PropertyInfo property)
    {
        if (property.PropertyType == typeof(string))
        {
            return _marker;
        }

        return property.PropertyType.IsValueType
            ? Activator.CreateInstance(property.PropertyType)
            : null;
    }
}
