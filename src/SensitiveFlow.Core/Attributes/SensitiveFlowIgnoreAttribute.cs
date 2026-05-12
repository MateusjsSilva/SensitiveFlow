namespace SensitiveFlow.Core.Attributes;

/// <summary>
/// Suppresses SensitiveFlow analyzer diagnostics on the target member.
/// Use sparingly and only when you are certain the data is not sensitive
/// despite matching a pattern that would normally trigger a diagnostic.
/// </summary>
/// <remarks>
/// This attribute tells analyzers to skip the decorated member entirely.
/// Prefer <see cref="AllowSensitiveLoggingAttribute"/> when you need to
/// log a sensitive value intentionally — it requires a justification.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveFlowIgnoreAttribute : Attribute
{
}
