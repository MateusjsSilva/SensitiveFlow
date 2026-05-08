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
    /// Secret key used for HMAC-SHA256. Must encode to at least 32 UTF-8 bytes to match
    /// the SHA-256 digest size and prevent brute-force attacks on the key. For pure ASCII
    /// keys this means 32+ characters; for Unicode keys the byte length differs from the
    /// character count.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secretKey"/> is null, whitespace, or encodes to fewer than 32 UTF-8 bytes.</exception>
    public HmacPseudonymizer(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        _keyBytes = Encoding.UTF8.GetBytes(secretKey);
        if (_keyBytes.Length < 32)
        {
            throw new ArgumentException(
                $"Secret key must encode to at least 32 UTF-8 bytes (got {_keyBytes.Length}) to match the SHA-256 digest size.",
                nameof(secretKey));
        }
    }

    /// <inheritdoc />
    public bool CanPseudonymize(string value) => !string.IsNullOrEmpty(value);

    /// <inheritdoc />
    public string Pseudonymize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var data = Encoding.UTF8.GetBytes(value);
        Span<byte> hash = stackalloc byte[32];
        HMACSHA256.HashData(_keyBytes, data, hash);
#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(hash);
#else
        return Convert.ToHexString(hash).ToLowerInvariant();
#endif
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
    /// <remarks>
    /// Returns a faulted task with <see cref="NotSupportedException"/> instead of throwing
    /// synchronously, so async callers observe the failure via the task and not via the call site.
    /// </remarks>
    public Task<string> ReverseAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromException<string>(new NotSupportedException(
            "HmacPseudonymizer does not support reversal. Use TokenPseudonymizer for reversible pseudonymization."));
}


