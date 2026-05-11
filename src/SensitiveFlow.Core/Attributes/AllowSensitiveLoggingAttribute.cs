namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Explicitly allows a sensitive member to be logged, suppressing SF0001.
/// A justification is required — this creates an audit trail for why the
/// exception was granted.
/// </summary>
/// <example>
/// <code>
/// [PersonalData(Category = DataCategory.Contact)]
/// [AllowSensitiveLogging(Justification = "Email is used as a correlation key in error logs only")]
/// public string Email { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class AllowSensitiveLoggingAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="justification">Why this sensitive value is allowed in logs. This is recorded for audit purposes.</param>
    public AllowSensitiveLoggingAttribute(string justification)
    {
        Justification = justification ?? throw new ArgumentNullException(nameof(justification));
    }

    /// <summary>
    /// The reason this sensitive value is permitted in log output.
    /// </summary>
    public string Justification { get; }
}
