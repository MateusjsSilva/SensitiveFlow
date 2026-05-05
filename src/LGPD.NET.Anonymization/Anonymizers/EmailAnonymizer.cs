using LGPD.NET.Core.Interfaces;

namespace LGPD.NET.Anonymization.Anonymizers;

/// <summary>
/// Anonymizes e-mail addresses by masking the local part while preserving the domain.
/// Example: <c>joao.silva@example.com</c> → <c>j***@example.com</c>.
/// The result is no longer personal data under Art. 12 of the LGPD.
/// </summary>
public sealed class EmailAnonymizer : IAnonymizer
{
    /// <inheritdoc />
    public bool CanAnonymize(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('@');

    /// <inheritdoc />
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
