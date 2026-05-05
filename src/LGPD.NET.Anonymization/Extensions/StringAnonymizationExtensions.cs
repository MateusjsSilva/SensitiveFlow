using LGPD.NET.Anonymization.Anonymizers;
using LGPD.NET.Anonymization.Pseudonymizers;
using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Extensions;

/// <summary>
/// Convenience extensions for anonymizing and pseudonymizing string values inline.
/// Each method creates a short-lived anonymizer instance — suitable for one-off operations.
/// For high-throughput scenarios, instantiate and reuse the anonymizer classes directly.
/// </summary>
public static class StringAnonymizationExtensions
{
    /// <summary>Anonymizes a Brazilian CPF or CNPJ tax identifier.</summary>
    public static string AnonymizeTaxId(this string value) =>
        new BrazilianTaxIdAnonymizer().Anonymize(value);

    /// <summary>Anonymizes an e-mail address, preserving the domain.</summary>
    public static string AnonymizeEmail(this string value) =>
        new EmailAnonymizer().Anonymize(value);

    /// <summary>Anonymizes a phone number, preserving the last two digits.</summary>
    public static string AnonymizePhone(this string value) =>
        new PhoneAnonymizer().Anonymize(value);

    /// <summary>Anonymizes a personal name, keeping the first letter of each word.</summary>
    public static string AnonymizeName(this string value) =>
        new NameAnonymizer().Anonymize(value);

    /// <summary>Anonymizes an IP address by zeroing the host portion.</summary>
    public static string AnonymizeIp(this string value) =>
        new IpAnonymizer().Anonymize(value);

    /// <summary>
    /// Pseudonymizes a value using a token backed by the provided <see cref="ITokenStore"/>.
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
    /// <param name="secretKey">Secret key for HMAC.</param>
    public static string PseudonymizeHmac(this string value, string secretKey) =>
        new HmacPseudonymizer(secretKey).Pseudonymize(value);
}
