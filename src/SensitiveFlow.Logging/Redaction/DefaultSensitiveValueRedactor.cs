using SensitiveFlow.Core.Profiles;

namespace SensitiveFlow.Logging.Redaction;

/// <summary>
/// Default redactor that replaces any sensitive value with a fixed marker.
/// </summary>
public sealed class DefaultSensitiveValueRedactor : ISensitiveValueRedactor
{
    private readonly string _marker;

    /// <summary>
    /// Initializes a new instance using the given replacement marker.
    /// Defaults to <see cref="SensitiveFlowDefaults.RedactedPlaceholder"/>.
    /// </summary>
    public DefaultSensitiveValueRedactor(string marker = SensitiveFlowDefaults.RedactedPlaceholder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        _marker = marker;
    }

    /// <inheritdoc />
    public string Redact(string value) => _marker;
}
