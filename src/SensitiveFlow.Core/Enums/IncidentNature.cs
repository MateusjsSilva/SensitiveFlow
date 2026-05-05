namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Nature of a personal data security incident.
/// </summary>
public enum IncidentNature
{
    /// <summary>Unspecified incident nature.</summary>
    Other = 0,

    /// <summary>Unauthorized access to systems or personal data.</summary>
    UnauthorizedAccess,

    /// <summary>Accidental disclosure of personal data.</summary>
    AccidentalDisclosure,

    /// <summary>Loss of personal data or media containing personal data.</summary>
    DataLoss,

    /// <summary>Theft or exfiltration of personal data.</summary>
    DataTheft,

    /// <summary>Ransomware incident affecting personal data.</summary>
    Ransomware,

    /// <summary>Compromise of a system that processes personal data.</summary>
    SystemCompromise,

    /// <summary>Improper disposal of personal data.</summary>
    ImproperDisposal
}

