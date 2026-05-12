namespace SensitiveFlow.Core.Policies;

/// <summary>
/// Fluent builder for a single SensitiveFlow policy rule.
/// </summary>
public sealed class SensitiveFlowPolicyRuleBuilder
{
    private SensitiveFlowPolicyAction _actions;

    internal SensitiveFlowPolicyRuleBuilder(SensitiveFlowPolicyRule seed)
    {
        Rule = seed;
        _actions = seed.Actions;
    }

    internal SensitiveFlowPolicyRule Rule { get; private set; }

    /// <summary>Requests log masking for the category.</summary>
    public SensitiveFlowPolicyRuleBuilder MaskInLogs()
    {
        return Add(SensitiveFlowPolicyAction.MaskInLogs);
    }

    /// <summary>Requests JSON redaction for the category.</summary>
    public SensitiveFlowPolicyRuleBuilder RedactInJson()
    {
        return Add(SensitiveFlowPolicyAction.RedactInJson);
    }

    /// <summary>Requests JSON omission for the category.</summary>
    public SensitiveFlowPolicyRuleBuilder OmitInJson()
    {
        return Add(SensitiveFlowPolicyAction.OmitInJson);
    }

    /// <summary>Requests audit entries when values in the category change.</summary>
    public SensitiveFlowPolicyRuleBuilder AuditOnChange()
    {
        return Add(SensitiveFlowPolicyAction.AuditOnChange);
    }

    /// <summary>Requires audit support for this category.</summary>
    public SensitiveFlowPolicyRuleBuilder RequireAudit()
    {
        return Add(SensitiveFlowPolicyAction.RequireAudit);
    }

    private SensitiveFlowPolicyRuleBuilder Add(SensitiveFlowPolicyAction action)
    {
        _actions |= action;
        Rule = Rule with { Actions = _actions };
        return this;
    }
}

