namespace SensitiveFlow.Anonymization.Strategies;

/// <summary>
/// Strategy that transforms a string value into a masked or anonymized form.
/// </summary>
public interface IMaskStrategy
{
    /// <summary>Applies the masking strategy to the specified value.</summary>
    /// <param name="value">Value to mask.</param>
    /// <returns>The masked value.</returns>
    string Apply(string value);
}

