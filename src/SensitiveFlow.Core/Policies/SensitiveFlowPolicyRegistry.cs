using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Policies;

/// <summary>
/// Registry of category-level policy rules.
/// </summary>
public sealed class SensitiveFlowPolicyRegistry
{
    private readonly Dictionary<DataCategory, SensitiveFlowPolicyRuleBuilder> _personal = [];
    private readonly Dictionary<SensitiveDataCategory, SensitiveFlowPolicyRuleBuilder> _sensitive = [];

    /// <summary>Starts or updates a rule for a personal data category.</summary>
    public SensitiveFlowPolicyRuleBuilder ForCategory(DataCategory category)
    {
        if (!_personal.TryGetValue(category, out var builder))
        {
            builder = new SensitiveFlowPolicyRuleBuilder(new SensitiveFlowPolicyRule
            {
                IsSensitiveCategory = false,
                Category = category,
                Actions = SensitiveFlowPolicyAction.None,
            });
            _personal.Add(category, builder);
        }

        return builder;
    }

    /// <summary>Starts or updates a rule for a sensitive data category.</summary>
    public SensitiveFlowPolicyRuleBuilder ForSensitiveCategory(SensitiveDataCategory category)
    {
        if (!_sensitive.TryGetValue(category, out var builder))
        {
            builder = new SensitiveFlowPolicyRuleBuilder(new SensitiveFlowPolicyRule
            {
                IsSensitiveCategory = true,
                SensitiveCategory = category,
                Actions = SensitiveFlowPolicyAction.None,
            });
            _sensitive.Add(category, builder);
        }

        return builder;
    }

    /// <summary>Gets all configured policy rules.</summary>
    public IReadOnlyList<SensitiveFlowPolicyRule> Rules =>
        _personal.Values.Concat(_sensitive.Values).Select(static b => b.Rule).ToArray();

    /// <summary>Finds a rule for a personal data category.</summary>
    public SensitiveFlowPolicyRule? Find(DataCategory category)
    {
        return _personal.TryGetValue(category, out var builder) ? builder.Rule : null;
    }

    /// <summary>Finds a rule for a sensitive data category.</summary>
    public SensitiveFlowPolicyRule? Find(SensitiveDataCategory category)
    {
        return _sensitive.TryGetValue(category, out var builder) ? builder.Rule : null;
    }
}

