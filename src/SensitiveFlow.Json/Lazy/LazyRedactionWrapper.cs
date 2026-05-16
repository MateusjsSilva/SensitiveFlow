namespace SensitiveFlow.Json.Lazy;

/// <summary>
/// Defers redaction processing until actual serialization (ToString/serializer access).
/// </summary>
/// <remarks>
/// Wraps the original value and applies masking/redaction on first string access.
/// Useful for large object graphs where many properties are never serialized.
/// </remarks>
/// <typeparam name="T">The type of the wrapped value.</typeparam>
public sealed class LazyRedactionWrapper<T>
{
    private readonly T? _originalValue;
    private readonly Func<T?, string>? _redactionFunc;
    private string? _cachedRedactedValue;
    private bool _hasBeenResolved;

    /// <summary>
    /// Creates a wrapper around a value with deferred redaction.
    /// </summary>
    /// <param name="originalValue">The original unredacted value.</param>
    /// <param name="redactionFunc">The function to apply for redaction. If null, original ToString() is used.</param>
    public LazyRedactionWrapper(T? originalValue, Func<T?, string>? redactionFunc = null)
    {
        _originalValue = originalValue;
        _redactionFunc = redactionFunc;
    }

    /// <summary>
    /// Gets the original unredacted value.
    /// </summary>
    public T? OriginalValue => _originalValue;

    /// <summary>
    /// Gets whether redaction has already been resolved.
    /// </summary>
    public bool IsResolved => _hasBeenResolved;

    /// <summary>
    /// Gets the redacted string representation, computing it on first access.
    /// </summary>
    public string GetRedactedValue()
    {
        if (!_hasBeenResolved)
        {
            _cachedRedactedValue = _redactionFunc?.Invoke(_originalValue) ?? _originalValue?.ToString() ?? string.Empty;
            _hasBeenResolved = true;
        }

        return _cachedRedactedValue ?? string.Empty;
    }

    /// <summary>
    /// Returns the redacted string representation.
    /// </summary>
    public override string ToString() => GetRedactedValue();
}
