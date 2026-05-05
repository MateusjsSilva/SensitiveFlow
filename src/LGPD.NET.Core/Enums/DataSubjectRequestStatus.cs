namespace LGPD.NET.Core.Enums;

/// <summary>
/// Lifecycle status of a data subject rights request.
/// </summary>
public enum DataSubjectRequestStatus
{
    /// <summary>The request was received and is awaiting handling.</summary>
    Open = 0,

    /// <summary>The request is being processed.</summary>
    InProgress,

    /// <summary>The request was completed.</summary>
    Completed,

    /// <summary>The request was rejected with justification.</summary>
    Rejected,

    /// <summary>The request was cancelled.</summary>
    Cancelled,

    /// <summary>The request exceeded its expected response window.</summary>
    Expired
}
