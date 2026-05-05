using System.Text.RegularExpressions;
using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Masking;

/// <summary>
/// Masks e-mail addresses by replacing the local part with asterisks, keeping only the first character.
/// Example: <c>joao.silva@example.com</c> → <c>j*********@example.com</c>.
/// </summary>
/// <remarks>
/// <b>This is masking, not anonymization.</b> The domain and first character remain visible,
/// which may be sufficient to re-identify the person in many contexts.
/// The result remains personal data and all LGPD obligations apply.
/// Use this class to reduce accidental exposure in UIs or logs — not as a compliance measure.
/// </remarks>
public sealed class EmailMasker : IMasker
{
    // Requires exactly one @, non-empty local part, and a domain with at least one dot.
    private static readonly Regex ValidEmail =
        new(@"^[^@]+@[^@]+\.[^@]+$", RegexOptions.Compiled);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a well-formed e-mail
    /// with exactly one <c>@</c> and a non-empty domain containing a dot.
    /// </summary>
    public bool CanMask(string value) =>
        !string.IsNullOrWhiteSpace(value) && ValidEmail.IsMatch(value);

    /// <summary>
    /// Masks the local part of <paramref name="value"/>, keeping only the first character.
    /// Returns the original string unchanged when <see cref="CanMask"/> returns <see langword="false"/>.
    /// </summary>
    public string Mask(string value)
    {
        if (!CanMask(value))
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
