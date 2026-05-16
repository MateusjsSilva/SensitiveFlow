using SensitiveFlow.Anonymization.Extensions;

namespace SensitiveFlow.Json.Masking;

/// <summary>
/// Registry for JSON masking strategies with built-in implementations.
/// </summary>
public class JsonMaskingStrategyRegistry
{
    private readonly Dictionary<string, IJsonMaskingStrategy> _strategies;

    /// <summary>
    /// Creates a new registry with built-in strategies registered.
    /// </summary>
    public JsonMaskingStrategyRegistry()
    {
        _strategies = new(StringComparer.OrdinalIgnoreCase)
        {
            { "email", new EmailMaskingStrategy() },
            { "phone", new PhoneMaskingStrategy() },
            { "creditcard", new CreditCardMaskingStrategy() },
            { "ssn", new SsnMaskingStrategy() },
            { "ipaddress", new IpAddressMaskingStrategy() }
        };
    }

    /// <summary>
    /// Gets a masking strategy by name.
    /// </summary>
    /// <param name="name">The strategy name (case-insensitive). Examples: "email", "phone", "creditcard", "ssn", "ipaddress".</param>
    /// <returns>The strategy, or null if not found.</returns>
    public IJsonMaskingStrategy? GetStrategy(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        _strategies.TryGetValue(name, out var strategy);
        return strategy;
    }

    /// <summary>
    /// Attempts to get a masking strategy by name.
    /// </summary>
    /// <param name="name">The strategy name (case-insensitive).</param>
    /// <param name="strategy">The strategy if found; null otherwise.</param>
    /// <returns>True if the strategy was found; false otherwise.</returns>
    public bool TryGetStrategy(string name, out IJsonMaskingStrategy? strategy)
    {
        strategy = GetStrategy(name);
        return strategy != null;
    }

    /// <summary>
    /// Registers a custom masking strategy.
    /// </summary>
    /// <param name="name">The strategy name (case-insensitive).</param>
    /// <param name="strategy">The strategy implementation.</param>
    /// <exception cref="ArgumentNullException">Thrown when name or strategy is null.</exception>
    public void Register(string name, IJsonMaskingStrategy strategy)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (strategy == null)
        {
            throw new ArgumentNullException(nameof(strategy));
        }

        _strategies[name] = strategy;
    }

    private sealed class EmailMaskingStrategy : IJsonMaskingStrategy
    {
        public string Mask(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            return StringAnonymizationExtensions.MaskEmail(value);
        }
    }

    private sealed class PhoneMaskingStrategy : IJsonMaskingStrategy
    {
        public string Mask(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            return StringAnonymizationExtensions.MaskPhone(value);
        }
    }

    private sealed class CreditCardMaskingStrategy : IJsonMaskingStrategy
    {
        public string Mask(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length < 4)
            {
                return "****";
            }

            var lastFour = digits.Substring(digits.Length - 4);
            return $"****-****-****-{lastFour}";
        }
    }

    private sealed class SsnMaskingStrategy : IJsonMaskingStrategy
    {
        public string Mask(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length < 4)
            {
                return "***-**-****";
            }

            var lastFour = digits.Substring(digits.Length - 4);
            return $"***-**-{lastFour}";
        }
    }

    private sealed class IpAddressMaskingStrategy : IJsonMaskingStrategy
    {
        public string Mask(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            var parts = value.Split('.');
            if (parts.Length != 4)
            {
                return "***.***.***.**";
            }

            return $"***.***{(parts.Length > 2 ? $".{parts[2]}" : ".*")}{(parts.Length > 3 ? $".{parts[3]}" : ".**")}";
        }
    }
}
