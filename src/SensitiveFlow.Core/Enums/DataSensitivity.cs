namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Risk level assigned to personal or sensitive data.
/// </summary>
public enum DataSensitivity
{
    /// <summary>Low risk if exposed, usually because the value is already public or weakly identifying.</summary>
    Low = 0,

    /// <summary>Moderate risk if exposed.</summary>
    Medium,

    /// <summary>High risk if exposed.</summary>
    High,

    /// <summary>Critical risk if exposed, requiring the strictest handling available.</summary>
    Critical,
}

