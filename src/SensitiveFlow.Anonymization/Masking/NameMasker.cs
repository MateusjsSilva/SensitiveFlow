using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Anonymization.Masking;

/// <summary>
/// Masks personal names by keeping the first letter of each word and replacing the rest with asterisks.
/// Example: <c>João da Silva</c> → <c>J*** d* S****</c>.
/// </summary>
/// <remarks>
/// <b>This is masking, not anonymization.</b> The initial letter and word length remain visible,
/// which is often sufficient to re-identify a person, especially for uncommon names or when
/// combined with other fields. The result remains personal data and all privacy obligations apply.
/// Use this class to reduce accidental exposure in UIs or logs — not as a compliance measure.
/// For full anonymization of names, remove the field entirely or replace it
/// with a <c>TokenPseudonymizer</c> token.
/// </remarks>
public sealed class NameMasker : IMasker
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a non-empty, non-whitespace string.
    /// </summary>
    public bool CanMask(string value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Masks <paramref name="value"/> by keeping the first letter of each word.
    /// Returns the original string unchanged when <see cref="CanMask"/> returns <see langword="false"/>.
    /// </summary>
    public string Mask(string value)
    {
        if (!CanMask(value))
        {
            return value;
        }

        var words  = value.Split(' ', StringSplitOptions.None);
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



