namespace SensitiveFlow.AspNetCore.Diagnostics;

/// <summary>
/// Runtime state used by startup diagnostics to report how the ASP.NET Core
/// SensitiveFlow middleware was wired.
/// </summary>
public sealed class SensitiveFlowAspNetCorePipelineDiagnostics
{
    /// <summary>Gets whether <c>UseSensitiveFlowAudit()</c> was added to the pipeline.</summary>
    public bool AuditMiddlewareRegistered { get; private set; }

    /// <summary>
    /// Gets whether the audit middleware observed an already-authenticated user
    /// before it ran. This can indicate the middleware was placed after
    /// authentication.
    /// </summary>
    public bool ObservedAuthenticatedUserBeforeAuditMiddleware { get; private set; }

    /// <summary>Marks the audit middleware as registered in the pipeline.</summary>
    public void MarkAuditMiddlewareRegistered()
    {
        AuditMiddlewareRegistered = true;
    }

    /// <summary>Marks that the middleware ran after authentication for at least one request.</summary>
    public void MarkAuthenticatedUserObserved()
    {
        ObservedAuthenticatedUserBeforeAuditMiddleware = true;
    }
}
