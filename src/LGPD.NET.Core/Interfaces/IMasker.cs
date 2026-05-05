namespace LGPD.NET.Core.Interfaces;

/// <summary>
/// Masks a value to reduce its identifiability for display or logging purposes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Masking is not anonymization under Art. 12 of the LGPD.</b>
/// A masked value (e.g. <c>j*********@example.com</c>) retains enough structure
/// to allow re-identification in many contexts. The result remains personal data
/// and all LGPD obligations continue to apply.
/// </para>
/// <para>
/// Use maskers when you need to reduce accidental exposure in UIs, logs, or reports —
/// not as a substitute for pseudonymization or anonymization.
/// </para>
/// </remarks>
public interface IMasker
{
    /// <summary>
    /// Masks the specified value, reducing its identifiability.
    /// Returns the original value unchanged when <see cref="CanMask"/> returns <see langword="false"/>.
    /// </summary>
    /// <param name="value">Value to mask.</param>
    /// <returns>A masked representation of the value.</returns>
    string Mask(string value);

    /// <summary>
    /// Determines whether this masker can process the specified value.
    /// </summary>
    /// <param name="value">Value to evaluate.</param>
    /// <returns><see langword="true"/> when the value can be masked; otherwise <see langword="false"/>.</returns>
    bool CanMask(string value);
}
