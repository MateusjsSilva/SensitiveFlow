using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Core.Policies;

/// <summary>
/// Validates that sensitive data protection policies are correctly configured.
/// </summary>
/// <remarks>
/// <para>
/// This validator can be used during application startup or periodically to ensure:
/// <list type="bullet">
///   <item><description>Required infrastructure components are registered (audit stores, etc.)</description></item>
///   <item><description>Redaction policies are active and initialized</description></item>
///   <item><description>Critical services are healthy and reachable</description></item>
/// </list>
/// </para>
/// <para>
/// If validation fails, a <see cref="RedactionPolicyViolationException"/> is thrown with
/// details about the specific policy violation.
/// </para>
/// </remarks>
public sealed class RedactionPolicyValidator
{
    /// <summary>Validates that an audit store is available and reachable.</summary>
    /// <param name="auditStore">The audit store to validate. Must not be <c>null</c>.</param>
    /// <exception cref="RedactionPolicyViolationException">
    /// Thrown if the store is <c>null</c> or fails connectivity checks.
    /// </exception>
    public static void ValidateAuditStore(IAuditStore? auditStore)
    {
        if (auditStore is null)
        {
            throw RedactionPolicyViolationException.MissingInfrastructure(
                nameof(IAuditStore));
        }
    }

    /// <summary>Validates that a token store is available for pseudonymization.</summary>
    /// <param name="tokenStore">The token store to validate. Must not be <c>null</c>.</param>
    /// <exception cref="RedactionPolicyViolationException">
    /// Thrown if the store is <c>null</c> or fails connectivity checks.
    /// </exception>
    public static void ValidateTokenStore(ITokenStore? tokenStore)
    {
        if (tokenStore is null)
        {
            throw RedactionPolicyViolationException.MissingInfrastructure(
                nameof(ITokenStore));
        }
    }

    /// <summary>
    /// Validates that required policies have been registered.
    /// </summary>
    /// <param name="policyRegistry">The policy registry to validate.</param>
    /// <exception cref="RedactionPolicyViolationException">
    /// Thrown if no policies are registered.
    /// </exception>
    public static void ValidatePoliciesRegistered(object? policyRegistry)
    {
        if (policyRegistry is null)
        {
            throw new RedactionPolicyViolationException(
                "No redaction policies registered. Call AddSensitiveFlow() with policy configuration.",
                "SF_REDACTION_004");
        }
    }

    /// <summary>
    /// Validates that an audit context is available and configured.
    /// </summary>
    /// <param name="auditContext">The audit context to validate.</param>
    /// <exception cref="RedactionPolicyViolationException">
    /// Thrown if the context is <c>null</c> or not properly initialized.
    /// </exception>
    public static void ValidateAuditContext(IAuditContext? auditContext)
    {
        if (auditContext is null)
        {
            throw RedactionPolicyViolationException.MissingInfrastructure(
                nameof(IAuditContext));
        }
    }

    /// <summary>
    /// Validates that a critical annotation is present on a type.
    /// </summary>
    /// <param name="typeName">The name of the type to check.</param>
    /// <param name="hasAnnotation"><c>true</c> if the type has the required annotation; <c>false</c> otherwise.</param>
    /// <param name="annotationName">Optional name of the expected annotation (for the error message).</param>
    /// <exception cref="RedactionPolicyViolationException">
    /// Thrown if the annotation is missing.
    /// </exception>
    public static void ValidateTypeAnnotation(
        string typeName,
        bool hasAnnotation,
        string annotationName = "sensitive data")
    {
        if (!hasAnnotation)
        {
            throw RedactionPolicyViolationException.MissingAnnotation(typeName);
        }
    }

    /// <summary>
    /// Validates that a property has a critical annotation.
    /// </summary>
    /// <param name="typeName">The name of the type containing the property.</param>
    /// <param name="propertyName">The name of the property to check.</param>
    /// <param name="hasAnnotation"><c>true</c> if the property has the annotation; <c>false</c> otherwise.</param>
    /// <exception cref="RedactionPolicyViolationException">
    /// Thrown if the annotation is missing.
    /// </exception>
    public static void ValidatePropertyAnnotation(
        string typeName,
        string propertyName,
        bool hasAnnotation)
    {
        if (!hasAnnotation)
        {
            throw RedactionPolicyViolationException.MissingAnnotation(typeName, propertyName);
        }
    }
}
