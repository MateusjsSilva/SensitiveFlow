using SensitiveFlow.Json.Enums;

namespace SensitiveFlow.Json.Attributes;

/// <summary>
/// Overrides the global <see cref="JsonRedactionMode"/> for a single property.
/// Apply on a property already annotated with <c>[PersonalData]</c> or <c>[SensitiveData]</c>.
/// </summary>
/// <example>
/// <code>
/// [PersonalData(Category = DataCategory.Contact)]
/// [JsonRedaction(JsonRedactionMode.Mask)]
/// public string Email { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class JsonRedactionAttribute : Attribute
{
    /// <summary>Initializes a new <see cref="JsonRedactionAttribute"/> with the given mode.</summary>
    public JsonRedactionAttribute(JsonRedactionMode mode)
    {
        Mode = mode;
    }

    /// <summary>Mode that overrides the global default for this specific property.</summary>
    public JsonRedactionMode Mode { get; }
}
