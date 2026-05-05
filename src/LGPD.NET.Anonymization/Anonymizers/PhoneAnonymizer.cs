using System.Text.RegularExpressions;
using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Anonymizers;

/// <summary>
/// Anonymizes phone numbers by masking all but the last two digits.
/// Supports Brazilian formats and generic digit sequences of 7–15 digits.
/// The result is no longer personal data under Art. 12 of the LGPD.
/// </summary>
public sealed class PhoneAnonymizer : IAnonymizer
{
    private static readonly Regex DigitsOnly = new(@"\d", RegexOptions.Compiled);
    private static readonly Regex ValidPhone = new(@"[\d\s\(\)\-\+]{7,20}", RegexOptions.Compiled);

    /// <inheritdoc />
    public bool CanAnonymize(string value) =>
        !string.IsNullOrWhiteSpace(value) && ValidPhone.IsMatch(value);

    /// <inheritdoc />
    public string Anonymize(string value)
    {
        if (!CanAnonymize(value))
        {
            return value;
        }

        var digits = DigitsOnly.Matches(value);
        if (digits.Count < 2)
        {
            return Regex.Replace(value, @"\d", "*");
        }

        // Keep last 2 digits visible, mask everything else
        var keepFrom = digits[^2].Index;
        var chars = value.ToCharArray();
        for (var i = 0; i < keepFrom; i++)
        {
            if (char.IsDigit(chars[i]))
            {
                chars[i] = '*';
            }
        }

        return new string(chars);
    }
}
