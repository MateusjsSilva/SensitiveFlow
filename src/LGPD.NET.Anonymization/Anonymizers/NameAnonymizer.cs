using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Anonymizers;

/// <summary>
/// Anonymizes personal names by keeping the first letter of each word and masking the rest.
/// Example: <c>João da Silva</c> → <c>J*** d* S****</c>.
/// The result is no longer personal data under Art. 12 of the LGPD.
/// </summary>
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
