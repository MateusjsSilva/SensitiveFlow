using System.Collections.Concurrent;
using System.Reflection;
using SensitiveFlow.Core.Attributes;

namespace SensitiveFlow.Core.Reflection;

/// <summary>
/// Per-type cache of properties decorated with <see cref="PersonalDataAttribute"/>,
/// <see cref="SensitiveDataAttribute"/>, or <see cref="RetentionDataAttribute"/>.
/// Reflection happens once per type — subsequent lookups return the cached
/// <see cref="PropertyInfo"/> arrays.
/// </summary>
public static class SensitiveMemberCache
{
    private static readonly ConcurrentDictionary<Type, AnnotatedProperties> Cache = new();

    /// <summary>
    /// Returns properties of <paramref name="type"/> annotated with
    /// <see cref="PersonalDataAttribute"/> or <see cref="SensitiveDataAttribute"/>.
    /// </summary>
    public static IReadOnlyList<PropertyInfo> GetSensitiveProperties(Type type)
        => GetOrAdd(type).Sensitive;

    /// <summary>
    /// Returns properties of <paramref name="type"/> annotated with
    /// <see cref="RetentionDataAttribute"/>, paired with the resolved attribute instance.
    /// </summary>
    public static IReadOnlyList<RetentionProperty> GetRetentionProperties(Type type)
        => GetOrAdd(type).Retention;

    private static AnnotatedProperties GetOrAdd(Type type)
        => Cache.GetOrAdd(type, BuildEntry);

    private static AnnotatedProperties BuildEntry(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var sensitive = new List<PropertyInfo>();
        var retention = new List<RetentionProperty>();

        foreach (var property in properties)
        {
            if (Attribute.IsDefined(property, typeof(PersonalDataAttribute)) ||
                Attribute.IsDefined(property, typeof(SensitiveDataAttribute)))
            {
                sensitive.Add(property);
            }

            var retentionAttr = property.GetCustomAttribute<RetentionDataAttribute>();
            if (retentionAttr is not null)
            {
                retention.Add(new RetentionProperty(property, retentionAttr));
            }
        }

        return new AnnotatedProperties(sensitive, retention);
    }

    private sealed class AnnotatedProperties
    {
        public AnnotatedProperties(IReadOnlyList<PropertyInfo> sensitive, IReadOnlyList<RetentionProperty> retention)
        {
            Sensitive = sensitive;
            Retention = retention;
        }

        public IReadOnlyList<PropertyInfo> Sensitive { get; }
        public IReadOnlyList<RetentionProperty> Retention { get; }
    }
}

/// <summary>Pair of a property and its <see cref="RetentionDataAttribute"/>.</summary>
public sealed class RetentionProperty
{
    /// <summary>Initializes a new instance.</summary>
    public RetentionProperty(PropertyInfo property, RetentionDataAttribute attribute)
    {
        Property = property;
        Attribute = attribute;
    }

    /// <summary>Underlying property.</summary>
    public PropertyInfo Property { get; }

    /// <summary>Resolved retention attribute.</summary>
    public RetentionDataAttribute Attribute { get; }
}
