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
    private JsonRedactionMode? _redactionMode;

    /// <summary>Initializes a new <see cref="JsonRedactionAttribute"/>.</summary>
    public JsonRedactionAttribute()
    {
    }

    /// <summary>Initializes a new <see cref="JsonRedactionAttribute"/> with the given mode.</summary>
    public JsonRedactionAttribute(JsonRedactionMode mode)
    {
        _redactionMode = mode;
    }

    /// <summary>Mode that overrides the global default for this specific property.</summary>
    public JsonRedactionMode Mode => _redactionMode ?? JsonRedactionMode.Mask;

    /// <summary>
    /// Named-property alias for <see cref="Mode"/>. This supports attribute syntax such as
    /// <c>[JsonRedaction(RedactionMode = JsonRedactionMode.Mask)]</c>.
    /// </summary>
    public JsonRedactionMode RedactionMode
    {
        get => Mode;
        set => _redactionMode = value;
    }

    /// <summary>
    /// Number of leading characters to preserve when applying the generic JSON mask.
    /// Set to <c>0</c> to hide the entire value. When null, SensitiveFlow uses its
    /// default mask heuristics for names, e-mails, phones, and generic strings.
    /// </summary>
    public int PreservePrefixLength { get; set; } = -1;

    internal bool HasPreservePrefixLength => PreservePrefixLength >= 0;
}
