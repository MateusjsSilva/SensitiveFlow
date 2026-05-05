namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Core processing principles used in privacy governance.
/// </summary>
public enum ProcessingPrinciple
{
    /// <summary>Processing for legitimate, specific, explicit, and informed purposes.</summary>
    Purpose = 0,

    /// <summary>Processing compatible with the stated purposes.</summary>
    Adequacy,

    /// <summary>Processing limited to what is necessary.</summary>
    Necessity,

    /// <summary>Free and easy access by data subjects.</summary>
    FreeAccess,

    /// <summary>Accuracy, clarity, relevance, and updating of data.</summary>
    DataQuality,

    /// <summary>Clear, precise, and easily accessible information.</summary>
    Transparency,

    /// <summary>Technical and administrative security measures.</summary>
    Security,

    /// <summary>Prevention of harm due to processing.</summary>
    Prevention,

    /// <summary>No unlawful or abusive discriminatory processing.</summary>
    NonDiscrimination,

    /// <summary>Demonstration of effective compliance measures.</summary>
    Accountability
}


