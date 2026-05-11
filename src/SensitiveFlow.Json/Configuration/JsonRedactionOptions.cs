using SensitiveFlow.Json.Enums;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Profiles;

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
    /// fallback for non-string values in <see cref="JsonRedactionMode.Mask"/>.
    /// Defaults to <c>"[REDACTED]"</c>.
    /// </summary>
    public string RedactedPlaceholder { get; set; } = SensitiveFlowDefaults.RedactedPlaceholder;

    /// <summary>
    /// Optional category policy registry. When present, JSON actions from policies are used
    /// before <see cref="DefaultMode"/> and after per-property attributes.
    /// </summary>
    public SensitiveFlowPolicyRegistry? Policies { get; set; }
}
