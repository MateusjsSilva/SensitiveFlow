namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Irreversible anonymization. The resulting data may no longer be personal data, depending on re-identification risk.
/// </summary>
public interface IAnonymizer
{
    /// <summary>Anonymizes the specified value irreversibly.</summary>
    /// <param name="value">Value to anonymize.</param>
    /// <returns>An anonymized value.</returns>
    string Anonymize(string value);

    /// <summary>Determines whether the specified value can be anonymized by this implementation.</summary>
    /// <param name="value">Value to evaluate.</param>
    /// <returns><see langword="true" /> when the value can be anonymized; otherwise, <see langword="false" />.</returns>
    bool CanAnonymize(string value);
}

