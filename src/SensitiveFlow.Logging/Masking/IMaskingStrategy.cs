namespace SensitiveFlow.Logging.Masking;

/// <summary>
/// Pluggable masking strategy for sensitive field values in logs.
/// </summary>
public interface IMaskingStrategy
{
    /// <summary>
    /// Masks the given value according to the strategy.
    /// </summary>
    /// <param name="value">The original sensitive value.</param>
    /// <returns>The masked value safe to log.</returns>
    string Mask(string value);
}
