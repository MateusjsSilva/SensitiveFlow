namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Action to execute when a retention period expires.
/// </summary>
public enum RetentionPolicy
{
    /// <summary>Anonymize the data when retention expires.</summary>
    AnonymizeOnExpiration = 0,

    /// <summary>Delete the data when retention expires.</summary>
    DeleteOnExpiration,

    /// <summary>Block use of the data when retention expires.</summary>
    BlockOnExpiration,

    /// <summary>Notify the data owner or responsible party.</summary>
    NotifyOwner
}

