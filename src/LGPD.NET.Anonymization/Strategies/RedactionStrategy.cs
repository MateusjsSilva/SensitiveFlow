namespace LGPD.NET.Anonymization.Strategies;

/// <summary>
/// Replaces the entire value with a fixed redaction marker.
/// </summary>
public sealed class RedactionStrategy : IMaskStrategy
{
    private readonly string _marker;

    /// <summary>Initializes a new instance with the default marker <c>[REDACTED]</c>.</summary>
    public RedactionStrategy() : this("[REDACTED]") { }

    /// <summary>Initializes a new instance with a custom marker.</summary>
    /// <param name="marker">Text that replaces the original value.</param>
    public RedactionStrategy(string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        _marker = marker;
    }

    /// <inheritdoc />
    public string Apply(string value) => _marker;
}
