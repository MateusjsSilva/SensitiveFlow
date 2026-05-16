using System.Net;

namespace SensitiveFlow.AspNetCore.IpMasking;

/// <summary>
/// Static utility for masking IP addresses to protect privacy while maintaining some usability for logging.
/// </summary>
public static class IpMaskingHelper
{
    /// <summary>
    /// Masks an IP address by replacing the last octet (IPv4) or last group (IPv6) with a given suffix.
    /// </summary>
    /// <param name="ip">The IP address string to mask.</param>
    /// <param name="maskSuffix">The suffix to use for masking. Default is <c>"XXX"</c>.</param>
    /// <returns>
    /// The masked IP address if parsing succeeds, otherwise the original string unchanged.
    /// IPv4: <c>192.168.1.1</c> → <c>192.168.1.XXX</c>.
    /// IPv6: <c>fe80::1</c> → <c>fe80::XXX</c>.
    /// </returns>
    public static string Mask(string ip, string maskSuffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskSuffix);

        if (!IPAddress.TryParse(ip, out var address))
        {
            return ip;
        }

        return address.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => MaskIpv4(ip, maskSuffix),
            System.Net.Sockets.AddressFamily.InterNetworkV6 => MaskIpv6(ip, maskSuffix),
            _ => ip,
        };
    }

    private static string MaskIpv4(string ip, string maskSuffix)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4)
        {
            return ip;
        }

        parts[3] = maskSuffix;
        return string.Join(".", parts);
    }

    private static string MaskIpv6(string ip, string maskSuffix)
    {
        var lastColonIndex = ip.LastIndexOf(':');
        if (lastColonIndex == -1)
        {
            return ip;
        }

        return ip[..lastColonIndex] + ":" + maskSuffix;
    }
}
