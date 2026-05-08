using System.Security.Cryptography;
using System.Text;

namespace SensitiveFlow.Anonymization.Strategies;

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
    /// <remarks>
    /// When salted, this implementation uses HMAC-SHA256 with the salt as the key — this
    /// removes the ambiguity of plain string concatenation, where (<c>"salt", "value"</c>)
    /// and (<c>"saltv", "alue"</c>) would otherwise hash to the same input.
    /// </remarks>
    public string Apply(string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        Span<byte> hash = stackalloc byte[32];

        if (_salt is null)
        {
            SHA256.HashData(data, hash);
        }
        else
        {
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(_salt), data, hash);
        }

#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(hash);
#else
        return Convert.ToHexString(hash).ToLowerInvariant();
#endif
    }
}

