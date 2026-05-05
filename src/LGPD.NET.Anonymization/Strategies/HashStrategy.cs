using System.Security.Cryptography;
using System.Text;

namespace LGPD.NET.Anonymization.Strategies;

/// <summary>
/// Produces a one-way SHA-256 hash of the value, optionally salted.
/// The output is a hex string — irreversible and deterministic for equal inputs.
/// </summary>
public sealed class HashStrategy : IMaskStrategy
{
    private readonly string? _salt;

    /// <summary>Initializes a new instance without a salt.</summary>
    public HashStrategy() { }

    /// <summary>Initializes a new instance with a fixed salt to prevent rainbow-table attacks.</summary>
    /// <param name="salt">
    /// Salt prepended to the value before hashing. Must be at least 16 characters
    /// to provide adequate entropy against rainbow-table attacks.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="salt"/> is null, whitespace, or shorter than 16 characters.</exception>
    public HashStrategy(string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(salt);
        if (salt.Length < 16)
        {
            throw new ArgumentException("Salt must be at least 16 characters to provide adequate entropy.", nameof(salt));
        }

        _salt = salt;
    }

    /// <inheritdoc />
    public string Apply(string value)
    {
        var input = _salt is null ? value : _salt + value;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
