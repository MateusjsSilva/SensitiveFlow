namespace SensitiveFlow.TokenStore.EFCore.Salting;

/// <summary>
/// Defines a strategy for applying salt to a value before tokenization.
/// Salting ensures that the same raw value produces different tokens in different contexts.
/// </summary>
public interface ITokenSaltStrategy
{
    /// <summary>
    /// Applies salt to the given value using the provided context.
    /// </summary>
    /// <param name="value">The original value to salt.</param>
    /// <param name="context">Optional context (e.g., field name, category). May be <c>null</c>.</param>
    /// <returns>The salted value.</returns>
    string Apply(string value, string? context);
}
