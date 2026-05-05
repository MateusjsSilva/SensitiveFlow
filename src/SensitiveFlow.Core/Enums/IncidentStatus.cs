namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Lifecycle status of a security incident.
/// </summary>
public enum IncidentStatus
{
    /// <summary>The incident was detected.</summary>
    Detected = 0,

    /// <summary>The incident was assessed for scope and impact.</summary>
    Assessed,

    /// <summary>The incident was notified to the applicable parties.</summary>
    Notified,

    /// <summary>The incident was closed.</summary>
    Closed
}

