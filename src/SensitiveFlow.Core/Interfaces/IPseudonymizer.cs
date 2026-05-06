namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Reversible pseudonymization. The data remains personal and all privacy obligations apply .
/// </summary>
public interface IPseudonymizer
{
    /// <summary>Pseudonymizes the specified value reversibly.</summary>
    /// <param name="value">Value to pseudonymize.</param>
    /// <returns>A pseudonymized token or value.</returns>
    string Pseudonymize(string value);

    /// <summary>Pseudonymizes the specified value reversibly without blocking asynchronous stores.</summary>
    /// <param name="value">Value to pseudonymize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pseudonymized token or value.</returns>
    Task<string> PseudonymizeAsync(string value, CancellationToken cancellationToken = default);

    /// <summary>Reverses a pseudonymized token back to the original value when authorized.</summary>
    /// <param name="token">Pseudonymized token or value.</param>
    /// <returns>The original value.</returns>
    string Reverse(string token);

    /// <summary>Reverses a pseudonymized token back to the original value without blocking asynchronous stores.</summary>
    /// <param name="token">Pseudonymized token or value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The original value.</returns>
    Task<string> ReverseAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Determines whether the specified value can be pseudonymized by this implementation.</summary>
    /// <param name="value">Value to evaluate.</param>
    /// <returns><see langword="true" /> when the value can be pseudonymized; otherwise, <see langword="false" />.</returns>
    bool CanPseudonymize(string value);
}

