using SensitiveFlow.Json.Enums;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Core;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Json.Roles;
using SensitiveFlow.Json.Masking;
using SensitiveFlow.Json.Metrics;

namespace SensitiveFlow.Json.Configuration;

/// <summary>
/// Global configuration for the SensitiveFlow JSON redaction converter.
/// </summary>
public sealed class JsonRedactionOptions
{
    /// <summary>
    /// Default redaction mode applied to every property annotated with <c>[PersonalData]</c>
    /// or <c>[SensitiveData]</c> that does not declare its own <c>[JsonRedaction]</c> override.
    /// Defaults to <see cref="JsonRedactionMode.Mask"/>.
    /// </summary>
    public JsonRedactionMode DefaultMode { get; set; } = JsonRedactionMode.Mask;

    /// <summary>
    /// Replacement string used by <see cref="JsonRedactionMode.Redacted"/> and as the
    /// fallback placeholder for non-string values when
    /// <see cref="NonStringRedactionMode"/> is <see cref="JsonNonStringRedactionMode.Placeholder"/>.
    /// Defaults to <see cref="SensitiveFlowDefaults.RedactedPlaceholder"/>.
    /// </summary>
    public string RedactedPlaceholder { get; set; } = SensitiveFlowDefaults.RedactedPlaceholder;

    /// <summary>
    /// Controls how annotated non-string values such as numbers, dates, booleans, and
    /// collections are represented when redacted in JSON. Defaults to
    /// <see cref="JsonNonStringRedactionMode.Null"/> to avoid emitting fake values such
    /// as <c>0</c> or leaking magnitude.
    /// </summary>
    public JsonNonStringRedactionMode NonStringRedactionMode { get; set; } = JsonNonStringRedactionMode.Null;

    /// <summary>
    /// Optional category policy registry. When present, JSON actions from policies are used
    /// before <see cref="DefaultMode"/> and after per-property attributes.
    /// </summary>
    public SensitiveFlowPolicyRegistry? Policies { get; set; }

    /// <summary>
    /// When <see langword="true"/>, redacted values include metadata annotation indicating
    /// the original property type (e.g., "Email", "Date") and redaction action. This helps
    /// consumers understand what was redacted without seeing the value. Defaults to <see langword="false"/>
    /// for backward compatibility; set to <see langword="true"/> when API contracts need to reflect
    /// type information.
    /// </summary>
    /// <remarks>
    /// When enabled, redacted values become objects:
    /// <code>
    /// "email": { "__redacted__": true, "type": "String", "action": "Mask" }
    /// </code>
    /// Instead of:
    /// <code>
    /// "email": "[REDACTED]"
    /// </code>
    /// </remarks>
    public bool IncludeRedactionMetadata { get; set; } = false;

    /// <summary>
    /// Optional resolver for determining which <see cref="RedactionContext"/> applies during serialization.
    /// When null, <see cref="RedactionContext.ApiResponse"/> is used. When provided, allows role-based
    /// or context-specific redaction rules (e.g., Admin vs Customer views).
    /// </summary>
    public IRedactionContextResolver? ContextResolver { get; set; }

    /// <summary>
    /// Optional registry of custom masking strategies for JSON properties.
    /// When populated, property-specific masks can reference named strategies via
    /// <c>[JsonRedaction(CustomStrategy = "strategyName")]</c>. Defaults to null.
    /// </summary>
    public JsonMaskingStrategyRegistry? MaskingStrategies { get; set; }

    /// <summary>
    /// When <see langword="true"/>, enables lazy redaction wrapping that defers masking until
    /// actual serialization occurs. Beneficial for large object graphs where many properties
    /// are never serialized. Defaults to <see langword="false"/>.
    /// </summary>
    public bool EnableLazyRedaction { get; set; } = false;

    /// <summary>
    /// Optional metrics collector for tracking JSON redaction operations.
    /// When provided, records counters and histograms for redaction events, duration,
    /// and property serialization. Defaults to null.
    /// </summary>
    public IJsonRedactionMetricsCollector? MetricsCollector { get; set; }
}
