namespace SensitiveFlow.TokenStore.EFCore.Salting;

/// <summary>
/// No-op salt strategy that returns the value unchanged.
/// This is the default strategy when no salting is configured.
/// </summary>
public sealed class PlainTextSaltStrategy : ITokenSaltStrategy
{
    /// <summary>
    /// Returns the value unchanged, ignoring context.
    /// </summary>
    /// <param name="value">The original value.</param>
    /// <param name="context">Ignored.</param>
    /// <returns>The value unchanged.</returns>
    public string Apply(string value, string? context)
    {
        return value;
    }
}
