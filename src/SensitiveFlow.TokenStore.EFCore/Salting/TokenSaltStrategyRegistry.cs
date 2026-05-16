using System.Collections.Concurrent;

namespace SensitiveFlow.TokenStore.EFCore.Salting;

/// <summary>
/// Thread-safe registry of named token salt strategies.
/// Allows applications to register custom strategies and retrieve them by name.
/// </summary>
public sealed class TokenSaltStrategyRegistry
{
    private static readonly Lazy<TokenSaltStrategyRegistry> _instance =
        new(static () => new TokenSaltStrategyRegistry());

    /// <summary>
    /// Gets the global singleton registry instance.
    /// </summary>
    public static TokenSaltStrategyRegistry Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, ITokenSaltStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenSaltStrategyRegistry"/> class
    /// with a default <see cref="PlainTextSaltStrategy"/>.
    /// </summary>
    public TokenSaltStrategyRegistry()
    {
        _strategies = new ConcurrentDictionary<string, ITokenSaltStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            ["plaintext"] = new PlainTextSaltStrategy(),
            ["prefix"] = new PrefixSaltStrategy(),
        };
    }

    /// <summary>
    /// Registers a strategy under the given name, replacing any existing registration.
    /// </summary>
    /// <param name="name">The strategy name (case-insensitive).</param>
    /// <param name="strategy">The strategy instance.</param>
    public void Register(string name, ITokenSaltStrategy strategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(strategy);

        _strategies[name] = strategy;
    }

    /// <summary>
    /// Gets a registered strategy by name, or returns a default <see cref="PlainTextSaltStrategy"/>
    /// if no matching strategy is found.
    /// </summary>
    /// <param name="name">The strategy name (case-insensitive). If <c>null</c>, returns default.</param>
    /// <returns>The registered strategy, or default if not found.</returns>
    public ITokenSaltStrategy GetOrDefault(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return new PlainTextSaltStrategy();
        }

        return _strategies.TryGetValue(name, out var strategy)
            ? strategy
            : new PlainTextSaltStrategy();
    }
}
