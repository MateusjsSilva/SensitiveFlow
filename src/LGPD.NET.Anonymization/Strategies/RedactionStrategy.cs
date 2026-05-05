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
    /// <param name="marker">Text that replaces the original value. Must be between 1 and 200 characters.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="marker"/> is null, whitespace, or exceeds 200 characters.</exception>
    public RedactionStrategy(string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        if (marker.Length > 200)
        {
            throw new ArgumentException("Marker must not exceed 200 characters.", nameof(marker));
        }

        _marker = marker;
    }

    /// <inheritdoc />
    public string Apply(string value) => _marker;
}
