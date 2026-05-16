namespace SensitiveFlow.TokenStore.EFCore.Expiration;

/// <summary>
/// Configuration options for token expiration behavior.
/// </summary>
public sealed class TokenExpirationOptions
{
    /// <summary>
    /// Gets or sets the default time-to-live for new tokens.
    /// When <c>null</c>, tokens never expire (default).
    /// </summary>
    public TimeSpan? DefaultTtl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether expired tokens should be automatically
    /// deleted during resolution. When <c>true</c>, resolution operations
    /// will purge expired records; when <c>false</c>, expired records remain until
    /// explicitly deleted by <see cref="TokenExpirationService{TContext}.PurgeExpiredAsync"/>.
    /// </summary>
    public bool PurgeOnAccess { get; set; }
}
