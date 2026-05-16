namespace SensitiveFlow.Logging.StructuredRedaction;

/// <summary>
/// Redacts sensitive keys in structured log property bags (dictionaries, scope state).
/// </summary>
public sealed class StructuredPropertyRedactor
{
    private readonly HashSet<string> _sensitivePropertyNames;
    private readonly string _redactedPlaceholder;

    /// <summary>
    /// Initializes a new instance with sensitive property names to redact.
    /// </summary>
    /// <param name="sensitivePropertyNames">Property names to redact (case-sensitive). Empty or null means no additional redaction.</param>
    /// <param name="redactedPlaceholder">Placeholder value for redacted properties. Defaults to "[REDACTED]".</param>
    public StructuredPropertyRedactor(IEnumerable<string>? sensitivePropertyNames = null, string redactedPlaceholder = "[REDACTED]")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redactedPlaceholder);
        _sensitivePropertyNames = new HashSet<string>(sensitivePropertyNames ?? [], StringComparer.Ordinal);
        _redactedPlaceholder = redactedPlaceholder;
    }

    /// <summary>
    /// Redacts values whose keys appear in the sensitive property names list.
    /// </summary>
    /// <param name="pairs">Key-value pairs from structured log state.</param>
    /// <returns>Pairs with sensitive keys redacted.</returns>
    public List<KeyValuePair<string, object?>> RedactPairs(IEnumerable<KeyValuePair<string, object?>> pairs)
    {
        var result = new List<KeyValuePair<string, object?>>();
        foreach (var pair in pairs)
        {
            if (_sensitivePropertyNames.Contains(pair.Key))
            {
                result.Add(new KeyValuePair<string, object?>(pair.Key, _redactedPlaceholder));
            }
            else
            {
                result.Add(pair);
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if any sensitive property names are configured.
    /// </summary>
    public bool HasSensitiveProperties => _sensitivePropertyNames.Count > 0;
}
