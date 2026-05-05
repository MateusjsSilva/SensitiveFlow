using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Anonymizers;

/// <summary>
/// Anonymizes e-mail addresses by masking the local part while preserving the domain.
/// Example: <c>joao.silva@example.com</c> → <c>j***@example.com</c>.
/// The result is no longer personal data under Art. 12 of the LGPD.
/// </summary>
public sealed class EmailAnonymizer : IAnonymizer
{
    // Requires exactly one @, non-empty local part, and a domain with at least one dot.
    private static readonly System.Text.RegularExpressions.Regex ValidEmail =
        new(@"^[^@]+@[^@]+\.[^@]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a well-formed e-mail
    /// with exactly one <c>@</c> and a non-empty domain containing a dot.
    /// </summary>
    public bool CanAnonymize(string value) =>
        !string.IsNullOrWhiteSpace(value) && ValidEmail.IsMatch(value);

    /// <summary>
    /// Anonymizes the local part of <paramref name="value"/>, keeping only the first character.
    /// Returns the original string unchanged when <see cref="CanAnonymize"/> returns <see langword="false"/>.
    /// </summary>
    public string Anonymize(string value)
    {
        if (!CanAnonymize(value))
        {
            return value;
        }

        var atIndex = value.IndexOf('@');
        var local   = value[..atIndex];
        var domain  = value[atIndex..];

        var masked = local.Length <= 1
            ? new string('*', local.Length)
            : local[0] + new string('*', local.Length - 1);

        return masked + domain;
    }
}
