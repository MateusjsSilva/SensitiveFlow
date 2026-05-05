using System.Text.RegularExpressions;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Anonymizers;

/// <summary>
/// Anonymizes Brazilian CPF (<c>000.000.000-00</c>) and CNPJ (<c>00.000.000/0000-00</c>) values
/// by replacing digits with asterisks while preserving punctuation structure.
/// The result is no longer personal data under applicable privacy regulations.
/// </summary>
public sealed class BrazilianTaxIdAnonymizer : IAnonymizer
{
    private static readonly Regex CpfPattern  = new(@"^\d{3}\.\d{3}\.\d{3}-\d{2}$", RegexOptions.Compiled);
    private static readonly Regex CnpjPattern = new(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$", RegexOptions.Compiled);
    private static readonly Regex RawPattern  = new(@"^\d{11}$|^\d{14}$", RegexOptions.Compiled);

    /// <inheritdoc />
    public bool CanAnonymize(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (CpfPattern.IsMatch(value) || CnpjPattern.IsMatch(value) || RawPattern.IsMatch(value));

    /// <inheritdoc />
    public string Anonymize(string value)
    {
        if (!CanAnonymize(value))
        {
            return value;
        }

        // Preserve punctuation, replace only digits
        return Regex.Replace(value, @"\d", "*");
    }
}


