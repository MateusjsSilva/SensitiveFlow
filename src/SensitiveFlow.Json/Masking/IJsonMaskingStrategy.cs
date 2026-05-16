namespace SensitiveFlow.Json.Masking;

/// <summary>
/// Strategy for masking sensitive values during JSON serialization.
/// </summary>
public interface IJsonMaskingStrategy
{
    /// <summary>
    /// Masks the provided value according to this strategy.
    /// </summary>
    /// <param name="value">The value to mask. May be null or empty.</param>
    /// <returns>The masked representation of the value.</returns>
    string Mask(string? value);
}
