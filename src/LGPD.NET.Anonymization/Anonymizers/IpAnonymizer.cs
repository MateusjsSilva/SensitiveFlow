using System.Net;
using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Anonymizers;

/// <summary>
/// Anonymizes IP addresses by zeroing the host portion.
/// IPv4: last octet → <c>0</c> (e.g. <c>192.168.1.10</c> → <c>192.168.1.0</c>).
/// IPv6: last 64 bits → <c>::</c> (Interface ID removed).
/// The result is no longer personal data under Art. 12 of the LGPD.
/// </summary>
public sealed class IpAnonymizer : IAnonymizer
{
    /// <inheritdoc />
    public bool CanAnonymize(string value) =>
        !string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value, out _);

    /// <inheritdoc />
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
            // Zero out the last 8 bytes (interface identifier)
            for (var i = 8; i < 16; i++)
            {
                bytes[i] = 0;
            }
            return new IPAddress(bytes).ToString();
        }

        return value;
    }
}
