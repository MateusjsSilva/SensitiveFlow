using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Anonymizers;

/// <summary>
/// Reduces the identifiability of personal names by keeping the first letter of each word
/// and masking the remaining characters.
/// Example: <c>João da Silva</c> → <c>J*** d* S****</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This technique is risk reduction, not anonymization under Art. 12 of the LGPD.</b>
/// Retaining the initial and the word length allows re-identification in many cases
/// (e.g. uncommon names, cross-referencing with other fields). The result remains
/// personal data and all LGPD obligations continue to apply.
/// </para>
/// <para>
/// Use this anonymizer for display purposes (e.g. showing a masked name in a UI) or as
/// one layer in a broader de-identification pipeline. For full Art. 12 anonymization of
/// names, remove the field entirely or replace it with a <c>TokenPseudonymizer</c> token.
/// </para>
/// </remarks>
public sealed class NameAnonymizer : IAnonymizer
{
    /// <inheritdoc />
    public bool CanAnonymize(string value) => !string.IsNullOrWhiteSpace(value);

    /// <inheritdoc />
    public string Anonymize(string value)
    {
        if (!CanAnonymize(value))
        {
            return value;
        }

        var words = value.Split(' ', StringSplitOptions.None);
        var masked = new string[words.Length];

        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            masked[i] = word.Length <= 1
                ? new string('*', word.Length)
                : word[0] + new string('*', word.Length - 1);
        }

        return string.Join(' ', masked);
    }
}
