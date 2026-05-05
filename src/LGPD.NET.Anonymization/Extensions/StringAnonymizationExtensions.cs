using LGPD.NET.Anonymization.Anonymizers;
using LGPD.NET.Anonymization.Masking;
using LGPD.NET.Anonymization.Pseudonymizers;
using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Extensions;

/// <summary>
/// Convenience extensions for anonymizing, masking, and pseudonymizing string values inline.
/// Shared instances are reused across calls — safe for high-throughput scenarios.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anonymization</b> (<c>AnonymizeTaxId</c>) produces a result that is no longer personal data
/// under Art. 12 of the LGPD — the transform is irreversible and non-identifiable.
/// </para>
/// <para>
/// <b>Masking</b> (<c>MaskEmail</c>, <c>MaskPhone</c>, <c>MaskName</c>) reduces accidental exposure
/// for display or logging purposes but does <b>not</b> constitute anonymization. The result remains
/// personal data and all LGPD obligations apply.
/// </para>
/// <para>
/// IP addresses are not covered here. IP truncation does not constitute anonymization under Art. 12
/// of the LGPD. IP addresses must be pseudonymized using <see cref="TokenPseudonymizer"/> before
/// being stored in audit logs.
/// </para>
/// </remarks>
public static class StringAnonymizationExtensions
{
    private static readonly BrazilianTaxIdAnonymizer TaxIdAnonymizer = new();
    private static readonly EmailMasker              EmailMasker     = new();
    private static readonly PhoneMasker              PhoneMasker     = new();
    private static readonly NameMasker               NameMasker      = new();

    // ── Anonymization (Art. 12 — data leaves LGPD scope) ──────────────────────

    /// <summary>
    /// Anonymizes a Brazilian CPF or CNPJ tax identifier by replacing all digits with asterisks.
    /// The result is no longer personal data under Art. 12 of the LGPD.
    /// </summary>
    public static string AnonymizeTaxId(this string value) =>
        TaxIdAnonymizer.Anonymize(value);

    // ── Masking (risk reduction — data remains personal) ──────────────────────

    /// <summary>
    /// Masks an e-mail address by keeping only the first character of the local part.
    /// Example: <c>joao.silva@example.com</c> → <c>j*********@example.com</c>.
    /// <b>Not anonymization</b> — the result remains personal data.
    /// </summary>
    public static string MaskEmail(this string value) =>
        EmailMasker.Mask(value);

    /// <summary>
    /// Masks a phone number by hiding all but the last two digits.
    /// Example: <c>(11) 99999-8877</c> → <c>(**) *****-**77</c>.
    /// <b>Not anonymization</b> — the result remains personal data.
    /// </summary>
    public static string MaskPhone(this string value) =>
        PhoneMasker.Mask(value);

    /// <summary>
    /// Masks a personal name by keeping only the first letter of each word.
    /// Example: <c>João da Silva</c> → <c>J*** d* S****</c>.
    /// <b>Not anonymization</b> — the result remains personal data.
    /// </summary>
    public static string MaskName(this string value) =>
        NameMasker.Mask(value);

    // ── Pseudonymization (Art. 12, §3 — data remains personal) ───────────────

    /// <summary>
    /// Pseudonymizes a value using a reversible token backed by the provided <see cref="ITokenStore"/>.
    /// The store must be durable in production — see <see cref="ITokenStore"/> for details.
    /// </summary>
    /// <param name="value">Value to pseudonymize.</param>
    /// <param name="pseudonymizer">The <see cref="TokenPseudonymizer"/> instance backed by a durable store.</param>
    public static string Pseudonymize(this string value, TokenPseudonymizer pseudonymizer) =>
        pseudonymizer.Pseudonymize(value);

    /// <summary>
    /// Pseudonymizes a value using HMAC-SHA256 with the provided secret key (deterministic, non-reversible).
    /// </summary>
    /// <param name="value">Value to pseudonymize.</param>
    /// <param name="secretKey">Secret key for HMAC. Must be at least 32 characters.</param>
    public static string PseudonymizeHmac(this string value, string secretKey) =>
        new HmacPseudonymizer(secretKey).Pseudonymize(value);
}
