using System.Text.RegularExpressions;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Masking;

/// <summary>
/// Masks phone numbers by replacing all but the last two digits with asterisks.
/// Example: <c>(11) 99999-8877</c> → <c>(**) *****-**77</c>.
/// </summary>
/// <remarks>
/// <b>This is masking, not anonymization.</b> The last two digits and the formatting structure
/// remain visible, which may allow re-identification when combined with other fields.
/// The result remains personal data and all privacy obligations apply.
/// Use this class to reduce accidental exposure in UIs or logs — not as a compliance measure.
/// </remarks>
public sealed class PhoneMasker : IMasker
{
    private static readonly Regex DigitsOnly = new(@"\d", RegexOptions.Compiled);

    // Requires at least 7 total characters from the allowed set AND at least one digit.
    private static readonly Regex ValidPhone =
        new(@"^(?=.*\d)[\d\s\(\)\-\+]{7,20}$", RegexOptions.Compiled);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> looks like a phone number:
    /// 7–20 characters composed of digits, spaces, parentheses, hyphens, or plus signs,
    /// with at least one digit present.
    /// </summary>
    public bool CanMask(string value) =>
        !string.IsNullOrWhiteSpace(value) && ValidPhone.IsMatch(value);

    /// <summary>
    /// Masks all but the last two digits of <paramref name="value"/>.
    /// Returns the original string unchanged when <see cref="CanMask"/> returns <see langword="false"/>.
    /// </summary>
    public string Mask(string value)
    {
        if (!CanMask(value))
        {
            return value;
        }

        var digits = DigitsOnly.Matches(value);
        if (digits.Count < 2)
        {
            return Regex.Replace(value, @"\d", "*");
        }

        var keepFrom = digits[^2].Index;
        var chars    = value.ToCharArray();
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


