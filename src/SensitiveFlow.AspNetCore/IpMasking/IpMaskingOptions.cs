namespace SensitiveFlow.AspNetCore.IpMasking;

/// <summary>
/// Options for IP address masking instead of pseudonymization.
/// </summary>
public sealed class IpMaskingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether IP address masking is enabled.
    /// When <c>true</c>, the last octet of IPv4 addresses (or last group of IPv6) is masked
    /// instead of being pseudonymized with a reversible token.
    /// Default is <c>false</c> (use pseudonymization).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the suffix to replace masked octets/groups with.
    /// Default is <c>"XXX"</c>.
    /// Example: <c>"192.168.1.XXX"</c> for IPv4 or <c>"fe80::XXXX"</c> for IPv6.
    /// </summary>
    public string MaskSuffix { get; set; } = "XXX";
}
