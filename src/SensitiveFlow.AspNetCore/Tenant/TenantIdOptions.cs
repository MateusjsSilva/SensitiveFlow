namespace SensitiveFlow.AspNetCore.Tenant;

/// <summary>
/// Options for extracting tenant ID from claims or headers in multi-tenant scenarios.
/// </summary>
public sealed class TenantIdOptions
{
    /// <summary>
    /// Gets or sets the claim type to look for the tenant ID.
    /// Default is <c>"tid"</c> (standard Azure AD tenant claim).
    /// Set to <c>null</c> to skip claim extraction and only check headers.
    /// </summary>
    public string? ClaimName { get; set; } = "tid";

    /// <summary>
    /// Gets or sets the HTTP header name to read the tenant ID from if not found in claims.
    /// Default is <c>"X-Tenant-ID"</c>.
    /// Set to <c>null</c> to skip header extraction and only check claims.
    /// </summary>
    public string? HeaderName { get; set; } = "X-Tenant-ID";
}
