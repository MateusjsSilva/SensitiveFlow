using Microsoft.Extensions.Logging;
using SensitiveFlow.Logging.Configuration;
using SensitiveFlow.Logging.Redaction;

namespace SensitiveFlow.Logging.Loggers;

/// <summary>
/// <see cref="ILoggerProvider"/> that wraps every logger from an inner provider
/// with a <see cref="RedactingLogger"/>.
/// </summary>
public sealed class RedactingLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider _innerProvider;
    private readonly ISensitiveValueRedactor _redactor;
    private readonly SensitiveLoggingOptions _options;

    /// <summary>Initializes a new instance of <see cref="RedactingLoggerProvider"/>.</summary>
    public RedactingLoggerProvider(
        ILoggerProvider innerProvider,
        ISensitiveValueRedactor redactor,
        SensitiveLoggingOptions? options = null)
    {
        _innerProvider = innerProvider;
        _redactor = redactor;
        _options = options ?? new SensitiveLoggingOptions();
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        var inner = _innerProvider.CreateLogger(categoryName);
        return new RedactingLogger(inner, _redactor, _options);
    }

    /// <inheritdoc />
    public void Dispose() => _innerProvider.Dispose();
}
