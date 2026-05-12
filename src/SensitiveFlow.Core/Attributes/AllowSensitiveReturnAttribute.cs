namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Suppresses analyzer warnings for intentionally returning sensitive data.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowSensitiveReturnAttribute : Attribute
{
    /// <summary>Initializes a new instance with the required justification.</summary>
    public AllowSensitiveReturnAttribute(string justification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        Justification = justification;
    }

    /// <summary>Gets the human-readable justification for allowing the return.</summary>
    public string Justification { get; }
}
