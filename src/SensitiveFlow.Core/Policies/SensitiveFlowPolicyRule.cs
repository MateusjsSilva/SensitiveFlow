using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Policies;

/// <summary>
/// Immutable policy rule for one personal or sensitive data category.
/// </summary>
public sealed record SensitiveFlowPolicyRule
{
    /// <summary>Gets whether the rule targets sensitive personal data.</summary>
    public required bool IsSensitiveCategory { get; init; }

    /// <summary>Gets the regular personal data category, when applicable.</summary>
    public DataCategory? Category { get; init; }

    /// <summary>Gets the sensitive data category, when applicable.</summary>
    public SensitiveDataCategory? SensitiveCategory { get; init; }

    /// <summary>Gets the configured actions.</summary>
    public SensitiveFlowPolicyAction Actions { get; init; }
}

