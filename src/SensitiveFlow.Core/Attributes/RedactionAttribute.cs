using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Declares contextual redaction behavior for a member.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class RedactionAttribute : Attribute
{
    /// <summary>Gets or sets the API response action.</summary>
    public OutputRedactionAction ApiResponse { get; set; } = OutputRedactionAction.None;

    /// <summary>Gets or sets the log action.</summary>
    public OutputRedactionAction Logs { get; set; } = OutputRedactionAction.None;

    /// <summary>Gets or sets the audit action.</summary>
    public OutputRedactionAction Audit { get; set; } = OutputRedactionAction.None;

    /// <summary>Gets or sets the export action.</summary>
    public OutputRedactionAction Export { get; set; } = OutputRedactionAction.None;

    /// <summary>Gets or sets the administrator view action (role-based redaction).</summary>
    public OutputRedactionAction AdminView { get; set; } = OutputRedactionAction.None;

    /// <summary>Gets or sets the support/helpdesk view action (role-based redaction).</summary>
    public OutputRedactionAction SupportView { get; set; } = OutputRedactionAction.None;

    /// <summary>Gets or sets the customer self-service view action (role-based redaction).</summary>
    public OutputRedactionAction CustomerView { get; set; } = OutputRedactionAction.None;

    /// <summary>Resolves the action for the given context.</summary>
    public OutputRedactionAction ForContext(RedactionContext context)
    {
        return context switch
        {
            RedactionContext.ApiResponse => ApiResponse,
            RedactionContext.Log => Logs,
            RedactionContext.Audit => Audit,
            RedactionContext.Export => Export,
            RedactionContext.AdminView => AdminView,
            RedactionContext.SupportView => SupportView,
            RedactionContext.CustomerView => CustomerView,
            _ => OutputRedactionAction.None,
        };
    }
}

