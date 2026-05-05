using System.Net;
using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Anonymizers;

/// <summary>
/// Anonymizes IP addresses by zeroing the host portion.
/// <list type="bullet">
///   <item><description>IPv4: last octet set to <c>0</c> — e.g. <c>192.168.1.10</c> → <c>192.168.1.0</c>.</description></item>
///   <item>
///     <description>
///       IPv6: last 64 bits (Interface Identifier) set to <c>::</c> — e.g. <c>2001:db8::1</c> → <c>2001:db8::</c>.
///       <para>
///         <b>Limitation:</b> zeroing the Interface ID is sufficient for global unicast addresses (/64 or wider prefix).
///         Link-local addresses (<c>fe80::/10</c>) and privacy/temporary addresses (RFC 4941) may still be
///         identifiable from the first 64 bits depending on ISP allocation. If full anonymization is required
///         for these address types, discard the address entirely instead of masking it.
///       </para>
///     </description>
///   </item>
/// </list>
/// The result is no longer personal data under Art. 12 of the LGPD for standard global unicast addresses.
/// </summary>
public sealed class IpAnonymizer : IAnonymizer
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a valid IPv4 or IPv6 address.
    /// </summary>
    public bool CanAnonymize(string value) =>
        !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value, out _);

    /// <summary>
    /// Anonymizes <paramref name="value"/> by zeroing the host portion.
    /// Returns the original string unchanged when <see cref="CanAnonymize"/> returns <see langword="false"/>.
    /// </summary>
    public string Anonymize(string value)
    {
        if (!IPAddress.TryParse(value, out var address))
        {
            return value;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            bytes[3] = 0;
            return new IPAddress(bytes).ToString();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            for (var i = 8; i < 16; i++)
            {
                bytes[i] = 0;
            }

            return new IPAddress(bytes).ToString();
        }

        return value;
    }
}
