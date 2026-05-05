namespace LGPD.NET.Core.Enums;

/// <summary>
/// Common purposes for personal data processing.
/// </summary>
public enum ProcessingPurpose
{
    /// <summary>Unspecified or custom processing purpose.</summary>
    Other = 0,

    /// <summary>Provision of a requested service.</summary>
    ServiceProvision,

    /// <summary>Marketing communication or campaigns.</summary>
    Marketing,

    /// <summary>Communication necessary to perform or manage a contract.</summary>
    ContractCommunication,

    /// <summary>Compliance with legal or regulatory obligations.</summary>
    LegalCompliance,

    /// <summary>Security, fraud prevention, or abuse prevention.</summary>
    Security,

    /// <summary>Research or statistical studies.</summary>
    Research,

    /// <summary>Analytics and product/service improvement.</summary>
    Analytics,

    /// <summary>Customer or user support.</summary>
    Support
}
