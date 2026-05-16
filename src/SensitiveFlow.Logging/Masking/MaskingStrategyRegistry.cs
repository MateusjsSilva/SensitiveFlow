namespace SensitiveFlow.Logging.Masking;

/// <summary>
/// Registry of named masking strategies with built-in strategies.
/// </summary>
public sealed class MaskingStrategyRegistry
{
    private readonly Dictionary<string, IMaskingStrategy> _strategies = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a registry with built-in strategies (phone, creditcard, ipaddress).
    /// </summary>
    public MaskingStrategyRegistry()
    {
        Register("phone", new PhoneMaskingStrategy());
        Register("creditcard", new CreditCardMaskingStrategy());
        Register("ipaddress", new IpAddressMaskingStrategy());
    }

    /// <summary>
    /// Registers a custom masking strategy.
    /// </summary>
    public void Register(string name, IMaskingStrategy strategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(strategy);
        _strategies[name] = strategy;
    }

    /// <summary>
    /// Attempts to get a masking strategy by name.
    /// </summary>
    public bool TryGetStrategy(string name, out IMaskingStrategy? strategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _strategies.TryGetValue(name, out strategy);
    }

    /// <summary>
    /// Gets a masking strategy by name, or returns null if not found.
    /// </summary>
    public IMaskingStrategy? GetStrategy(string name)
    {
        TryGetStrategy(name, out var strategy);
        return strategy;
    }
}

/// <summary>
/// Masks phone numbers: keeps only last 2 digits visible.
/// </summary>
public sealed class PhoneMaskingStrategy : IMaskingStrategy
{
    /// <inheritdoc />
    public string Mask(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var chars = value.ToCharArray();
        var digitsSeenFromEnd = 0;
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(chars[i]))
            {
                continue;
            }

            digitsSeenFromEnd++;
            if (digitsSeenFromEnd > 2)
            {
                chars[i] = '*';
            }
        }

        return new string(chars);
    }
}

/// <summary>
/// Masks credit card numbers: keeps only last 4 digits visible.
/// </summary>
public sealed class CreditCardMaskingStrategy : IMaskingStrategy
{
    /// <inheritdoc />
    public string Mask(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var digits = value.Where(char.IsDigit).ToList();
        if (digits.Count < 4)
        {
            return "****";
        }

        var visibleDigits = string.Concat(digits.TakeLast(4));
        return $"****-****-****-{visibleDigits}";
    }
}

/// <summary>
/// Masks IP addresses: redacts first 2 octets.
/// </summary>
public sealed class IpAddressMaskingStrategy : IMaskingStrategy
{
    /// <inheritdoc />
    public string Mask(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var parts = value.Split('.');
        if (parts.Length < 4)
        {
            return "***.***.***.***";
        }

        return $"***.***. {parts[2]}.{parts[3]}";
    }
}
