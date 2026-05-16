namespace SensitiveFlow.TokenStore.EFCore.Salting;

/// <summary>
/// Salt strategy that prepends the context as a prefix to the value, separated by a colon.
/// When context is <c>null</c>, the value is returned unchanged.
/// </summary>
public sealed class PrefixSaltStrategy : ITokenSaltStrategy
{
    /// <summary>
    /// Prepends the context to the value if context is non-null.
    /// </summary>
    /// <param name="value">The original value.</param>
    /// <param name="context">The context to use as a prefix. When <c>null</c>, value is returned unchanged.</param>
    /// <returns>The salted value: <c>context:value</c> or just <c>value</c> if context is null.</returns>
    public string Apply(string value, string? context)
    {
        if (string.IsNullOrEmpty(context))
        {
            return value;
        }

        return $"{context}:{value}";
    }
}
