using System.Security.Cryptography;
using System.Text;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Pseudonymizers;

/// <summary>
/// Deterministic pseudonymization via HMAC-SHA256 with a secret key.
/// The same input and key always produce the same token — enabling consistent
/// lookups without storing a mapping table.
/// This implementation is NOT reversible through <see cref="Reverse"/> unless the
/// caller maintains its own lookup; <see cref="Reverse"/> throws <see cref="NotSupportedException"/>.
/// The data remains personal and all privacy obligations apply.
/// </summary>
/// <remarks>
/// Use <see cref="TokenPseudonymizer"/> when you need true reversibility.
/// Use <see cref="HmacPseudonymizer"/> when you need consistent, key-controlled tokens
/// without storing a mapping table (e.g. for join keys across systems that share the secret).
/// </remarks>
public sealed class HmacPseudonymizer : IPseudonymizer
{
    private readonly byte[] _keyBytes;

    /// <summary>Initializes a new instance with the provided secret key.</summary>
    /// <param name="secretKey">
    /// Secret key used for HMAC-SHA256. Must be at least 32 characters to match the
    /// SHA-256 digest size and prevent brute-force attacks on the key.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secretKey"/> is null, whitespace, or shorter than 32 characters.</exception>
    public HmacPseudonymizer(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        if (secretKey.Length < 32)
        {
            throw new ArgumentException("Secret key must be at least 32 characters to match the SHA-256 digest size.", nameof(secretKey));
        }

        _keyBytes = Encoding.UTF8.GetBytes(secretKey);
    }

    /// <inheritdoc />
    public bool CanPseudonymize(string value) => !string.IsNullOrEmpty(value);

    /// <inheritdoc />
    public string Pseudonymize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var data = Encoding.UTF8.GetBytes(value);
        var hash = HMACSHA256.HashData(_keyBytes, data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <inheritdoc />
    public Task<string> PseudonymizeAsync(string value, CancellationToken cancellationToken = default)
        => Task.FromResult(Pseudonymize(value));

    /// <summary>
    /// Not supported — HMAC pseudonymization is deterministic but not reversible without a lookup table.
    /// Use <see cref="TokenPseudonymizer"/> for reversible pseudonymization.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public string Reverse(string token) =>
        throw new NotSupportedException(
            "HmacPseudonymizer does not support reversal. Use TokenPseudonymizer for reversible pseudonymization.");

    /// <inheritdoc />
    public Task<string> ReverseAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(Reverse(token));
}


