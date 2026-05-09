using System.Security.Cryptography;
using System.Text;

namespace SensitiveFlow.Anonymization.Comparison;

/// <summary>
/// Produces short, deterministic fingerprints for comparing sensitive values without
/// exposing them. Two equal inputs always yield the same fingerprint; different inputs
/// yield different fingerprints with overwhelming probability.
/// </summary>
/// <remarks>
/// <para>Use this when you need to answer questions like:</para>
/// <list type="bullet">
///   <item>"Did this customer's e-mail change between save A and save B?"</item>
///   <item>"Are these two records about the same person?" (same input → same fingerprint)</item>
///   <item>"Show a diff of two payloads in a log without exposing the original values."</item>
/// </list>
/// <para>
/// Fingerprints are <b>keyed</b> (HMAC-SHA256 with a secret) so an attacker cannot replay
/// known plaintexts to learn fingerprints. The output is a 16-character lowercase hex string
/// (64 bits of fingerprint) — short enough to log, long enough to make collisions vanishingly
/// rare for application-scale workloads.
/// </para>
/// <para>
/// <b>Not anonymization.</b> A determined attacker with the secret can still brute-force
/// small input domains (e.g. boolean values, low-cardinality enums). Apply the same access
/// controls you would to the raw value.
/// </para>
/// </remarks>
public sealed class DeterministicFingerprint
{
    private readonly byte[] _keyBytes;

    /// <summary>Initializes a new fingerprint generator with the given secret key.</summary>
    /// <param name="secretKey">
    /// Secret key. Must encode to at least 32 UTF-8 bytes (e.g. 32+ ASCII characters).
    /// Rotating the key invalidates every previously-issued fingerprint.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when the key is null, whitespace, or too short.</exception>
    public DeterministicFingerprint(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        _keyBytes = Encoding.UTF8.GetBytes(secretKey);
        if (_keyBytes.Length < 32)
        {
            throw new ArgumentException(
                $"Secret key must encode to at least 32 UTF-8 bytes (got {_keyBytes.Length}).",
                nameof(secretKey));
        }
    }

    /// <summary>
    /// Returns a 16-character lowercase hex fingerprint of <paramref name="value"/>.
    /// Returns an empty string for a null or empty input so equality checks remain consistent.
    /// </summary>
    public string Fingerprint(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var data = Encoding.UTF8.GetBytes(value);
        Span<byte> hash = stackalloc byte[32];
        HMACSHA256.HashData(_keyBytes, data, hash);

        // Take the first 8 bytes (64 bits). Encoding.GetString won't work — we need hex.
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    /// <summary>
    /// Returns <c>true</c> when both inputs produce the same fingerprint. Use this instead
    /// of comparing raw strings when you want the comparison to be safe to log.
    /// </summary>
    public bool AreEquivalent(string? left, string? right)
        => string.Equals(Fingerprint(left), Fingerprint(right), StringComparison.Ordinal);
}
