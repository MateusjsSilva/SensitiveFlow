namespace SensitiveFlow.TokenStore.EFCore.Audit;

/// <summary>
/// Enumerates the types of operations that can be audited on tokens.
/// </summary>
public enum TokenAuditOperation
{
    /// <summary>A new token was created.</summary>
    Created = 0,

    /// <summary>An existing token was resolved (pseudonym looked up).</summary>
    Resolved = 1,

    /// <summary>A token expired and was purged.</summary>
    Expired = 2,
}
