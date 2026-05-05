namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Rights request types available to data subjects under Art. 18 of the LGPD.
/// </summary>
public enum DataSubjectRequestType
{
    /// <summary>Confirmation of the existence of processing.</summary>
    Confirmation = 0,

    /// <summary>Access to personal data.</summary>
    Access,

    /// <summary>Correction of incomplete, inaccurate, or outdated data.</summary>
    Correction,

    /// <summary>Anonymization of unnecessary or excessive data.</summary>
    Anonymization,

    /// <summary>Blocking of unnecessary or excessive data.</summary>
    Blocking,

    /// <summary>Deletion of unnecessary or excessive data.</summary>
    Deletion,

    /// <summary>Data portability request.</summary>
    Portability,

    /// <summary>Information about entities with which data was shared.</summary>
    Information,

    /// <summary>Objection to processing.</summary>
    Objection
}

