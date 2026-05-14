using System.Collections.Concurrent;
using System.Reflection;
using SensitiveFlow.Core.Attributes;

namespace SensitiveFlow.Core.Reflection;

/// <summary>
/// Per-type cache of properties decorated with <see cref="PersonalDataAttribute"/>,
/// <see cref="SensitiveDataAttribute"/>, <see cref="RetentionDataAttribute"/>, or <see cref="RedactionAttribute"/>.
/// Reflection happens once per type — subsequent lookups return the cached
/// <see cref="PropertyInfo"/> arrays and attributes.
/// </summary>
public static class SensitiveMemberCache
{
    private static readonly ConcurrentDictionary<Type, AnnotatedProperties> Cache = new();
    private static readonly ConcurrentDictionary<Type, GeneratedSensitiveType> Generated = new();
    private static readonly ConcurrentDictionary<(Type, string), RedactionAttribute?> RedactionCache = new();

    /// <summary>
    /// Registers source-generated metadata for sensitive and retention members.
    /// </summary>
    public static void RegisterGeneratedMetadata(IEnumerable<GeneratedSensitiveType> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        foreach (var entry in metadata)
        {
            if (entry is null)
            {
                continue;
            }

            Generated.TryAdd(entry.Type, entry);
        }
    }

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

    /// <summary>
    /// Returns the <see cref="RedactionAttribute"/> for a property, if present.
    /// Results are cached per-type per-property to avoid repeated reflection.
    /// </summary>
    public static RedactionAttribute? GetRedactionAttribute(Type type, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var key = (type, propertyName);
        return RedactionCache.GetOrAdd(key, _ =>
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null)
            {
                return null;
            }

            return prop.GetCustomAttribute<RedactionAttribute>()
                ?? GetInterfaceAttribute<RedactionAttribute>(type, prop);
        });
    }

    private static AnnotatedProperties GetOrAdd(Type type)
        => Cache.GetOrAdd(type, BuildEntry);

    private static AnnotatedProperties BuildEntry(Type type)
    {
        if (Generated.TryGetValue(type, out var generated))
        {
            return BuildEntryFromGenerated(type, generated);
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var sensitive = new List<PropertyInfo>();
        var retention = new List<RetentionProperty>();

        foreach (var property in properties)
        {
            var hasPersonal = Attribute.IsDefined(property, typeof(PersonalDataAttribute))
                || HasInterfaceAttribute(type, property, typeof(PersonalDataAttribute));
            var hasSensitive = Attribute.IsDefined(property, typeof(SensitiveDataAttribute))
                || HasInterfaceAttribute(type, property, typeof(SensitiveDataAttribute));

            if (hasPersonal || hasSensitive)
            {
                sensitive.Add(property);
            }

            var retentionAttr = property.GetCustomAttribute<RetentionDataAttribute>()
                ?? GetInterfaceAttribute<RetentionDataAttribute>(type, property);
            if (retentionAttr is not null)
            {
                retention.Add(new RetentionProperty(property, retentionAttr));
            }
        }

        return new AnnotatedProperties(sensitive, retention);
    }

    private static bool HasInterfaceAttribute(Type type, PropertyInfo property, Type attributeType)
    {
        foreach (var iface in type.GetInterfaces())
        {
            var ifaceProp = iface.GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);
            if (ifaceProp is not null && Attribute.IsDefined(ifaceProp, attributeType))
            {
                return true;
            }
        }
        return false;
    }

    private static T? GetInterfaceAttribute<T>(Type type, PropertyInfo property) where T : Attribute
    {
        foreach (var iface in type.GetInterfaces())
        {
            var ifaceProp = iface.GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);
            var attr = ifaceProp?.GetCustomAttribute<T>();
            if (attr is not null)
            {
                return attr;
            }
        }
        return null;
    }

    private static AnnotatedProperties BuildEntryFromGenerated(Type type, GeneratedSensitiveType generated)
    {
        var sensitive = new List<PropertyInfo>();
        var retention = new List<RetentionProperty>();

        foreach (var name in generated.SensitivePropertyNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null)
            {
                sensitive.Add(property);
            }
        }

        foreach (var generatedRetention in generated.RetentionProperties)
        {
            var property = type.GetProperty(generatedRetention.PropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                continue;
            }

            var attr = new RetentionDataAttribute
            {
                Years = generatedRetention.Years,
                Months = generatedRetention.Months,
                Policy = generatedRetention.Policy,
            };

            retention.Add(new RetentionProperty(property, attr));
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
