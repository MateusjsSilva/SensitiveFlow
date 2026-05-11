namespace SensitiveFlow.Core.Enums;

/// <summary>
/// Built-in masking shape requested by output behavior attributes and assertions.
/// </summary>
public enum MaskKind
{
    /// <summary>Generic mask preserving only minimal structure.</summary>
    Generic = 0,

    /// <summary>Email-aware mask.</summary>
    Email,

    /// <summary>Phone-aware mask.</summary>
    Phone,

    /// <summary>Name-aware mask.</summary>
    Name,
}

