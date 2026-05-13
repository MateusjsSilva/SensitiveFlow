namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Exception thrown when a sensitive data protection policy is violated or fails.
/// </summary>
/// <remarks>
/// <para>
/// This exception is raised when redaction fails, required annotations are missing,
/// or a critical infrastructure component (audit store, token store, etc.) is unavailable
/// when needed for a sensitive operation.
/// </para>
/// <para>
/// <b>Example scenarios:</b>
/// <list type="bullet">
///   <item><description>Attempting to serialize a sensitive field without a JSON modifier registered</description></item>
///   <item><description>Logging a sensitive value when the logging redactor is not configured</description></item>
///   <item><description>Saving audit data when the audit store is unreachable</description></item>
///   <item><description>Missing required <see cref="SensitiveFlow.Core.Attributes.PersonalDataAttribute"/> on a return type</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class RedactionPolicyViolationException : SensitiveFlowException
{
    /// <summary>Initializes a new instance with a message and error code.</summary>
    public RedactionPolicyViolationException(string message, string code, Exception? innerException = null)
        : base(message, code, innerException)
    {
    }

    /// <summary>Initializes a new instance for a missing annotation.</summary>
    /// <param name="typeName">The type name missing the annotation.</param>
    /// <param name="propertyName">The property name missing the annotation, or <c>null</c> for type-level.</param>
    /// <returns>A new instance with a descriptive message and code <c>SF_REDACTION_001</c>.</returns>
    public static RedactionPolicyViolationException MissingAnnotation(string typeName, string? propertyName = null)
    {
        var message = propertyName is null
            ? $"Type '{typeName}' is missing required sensitive data annotations."
            : $"Property '{typeName}.{propertyName}' is missing required sensitive data annotation.";

        return new RedactionPolicyViolationException(message, "SF_REDACTION_001");
    }

    /// <summary>Initializes a new instance for missing infrastructure (e.g., audit store not available).</summary>
    /// <param name="infrastructureType">The type of infrastructure (e.g., "IAuditStore", "IPseudonymizer").</param>
    /// <param name="innerException">Optional inner exception.</param>
    /// <returns>A new instance with a descriptive message and code <c>SF_REDACTION_002</c>.</returns>
    public static RedactionPolicyViolationException MissingInfrastructure(
        string infrastructureType,
        Exception? innerException = null)
    {
        var message = $"Required infrastructure '{infrastructureType}' is not registered or available. " +
            "Register it via AddSensitiveFlow options.";

        return new RedactionPolicyViolationException(message, "SF_REDACTION_002", innerException);
    }

    /// <summary>Initializes a new instance for redaction operation failure.</summary>
    /// <param name="fieldName">The field that failed to redact.</param>
    /// <param name="operation">The operation being performed (e.g., "serialize", "log").</param>
    /// <param name="innerException">Optional inner exception.</param>
    /// <returns>A new instance with a descriptive message and code <c>SF_REDACTION_003</c>.</returns>
    public static RedactionPolicyViolationException RedactionFailed(
        string fieldName,
        string operation,
        Exception? innerException = null)
    {
        var message = $"Redaction failed for field '{fieldName}' during {operation}.";

        return new RedactionPolicyViolationException(message, "SF_REDACTION_003", innerException);
    }
}
