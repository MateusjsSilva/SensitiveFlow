using System.Reflection;

namespace SensitiveFlow.Retention.Services;

/// <summary>Options that control <see cref="RetentionExecutor"/> behavior.</summary>
public sealed class RetentionExecutorOptions
{
    private Func<object, PropertyInfo, object?>? _anonymousValueFactory;

    /// <summary>
    /// Resolver for the value used to overwrite an expired field when the policy is
    /// <c>AnonymizeOnExpiration</c>. Defaults to <see cref="DefaultAnonymousValue"/>.
    /// </summary>
    public Func<object, PropertyInfo, object?> AnonymousValueFactory
    {
        get => _anonymousValueFactory ?? DefaultAnonymousValue;
        set => _anonymousValueFactory = value;
    }

    /// <summary>
    /// Default placeholder used by <see cref="AnonymousValueFactory"/> for string properties.
    /// Value types collapse to <c>default(T)</c>.
    /// </summary>
    public string AnonymousStringMarker { get; set; } = "[ANONYMIZED]";

    /// <summary>
    /// Returns the type's anonymous default. Strings return <see cref="AnonymousStringMarker"/>,
    /// value types return <c>default(T)</c>, reference types return <c>null</c>.
    /// </summary>
    public object? DefaultAnonymousValue(object entity, PropertyInfo property)
    {
        if (property.PropertyType == typeof(string))
        {
            return AnonymousStringMarker;
        }

        return property.PropertyType.IsValueType
            ? Activator.CreateInstance(property.PropertyType)
            : null;
    }
}
