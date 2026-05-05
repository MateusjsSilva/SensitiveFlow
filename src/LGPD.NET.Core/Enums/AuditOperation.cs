namespace LGPD.NET.Core.Enums;

/// <summary>
/// Operations that can be recorded in an audit trail involving personal data.
/// </summary>
public enum AuditOperation
{
    /// <summary>Personal data was read or accessed.</summary>
    Access = 0,

    /// <summary>Personal data was created.</summary>
    Create,

    /// <summary>Personal data was updated.</summary>
    Update,

    /// <summary>Personal data was deleted.</summary>
    Delete,

    /// <summary>Personal data was anonymized.</summary>
    Anonymize,

    /// <summary>Personal data was pseudonymized.</summary>
    Pseudonymize,

    /// <summary>Personal data was exported.</summary>
    Export,

    /// <summary>Personal data was shared with a third party.</summary>
    Share,

    /// <summary>A consent or authorization was revoked.</summary>
    Revoke
}
