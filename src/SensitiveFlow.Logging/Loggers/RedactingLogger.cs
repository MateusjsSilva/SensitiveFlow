using Microsoft.Extensions.Logging;
using SensitiveFlow.Logging.Redaction;

namespace SensitiveFlow.Logging.Loggers;

/// <summary>
/// <see cref="ILogger"/> decorator that intercepts log messages and redacts any
/// value found inside <c>{[Sensitive]...}</c> placeholders before forwarding to
/// the inner logger.
/// <para>
/// Mark sensitive structured log parameters with the <c>[Sensitive]</c> prefix:
/// <code>
/// logger.LogInformation("User {[Sensitive]Email} logged in", email);
/// </code>
/// </para>
/// </summary>
public sealed class RedactingLogger : ILogger
{
    private readonly ILogger _inner;
    private readonly ISensitiveValueRedactor _redactor;

    /// <summary>Initializes a new instance of <see cref="RedactingLogger"/>.</summary>
    public RedactingLogger(ILogger inner, ISensitiveValueRedactor redactor)
    {
        _inner = inner;
        _redactor = redactor;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _inner.BeginScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        _inner.Log(logLevel, eventId, state, exception, (s, ex) =>
        {
            var message = formatter(s, ex);
            return RedactMessage(message);
        });
    }

    internal string RedactMessage(string message)
    {
        // Replace occurrences of {[Sensitive]<anything>} in the formatted message
        // with the redacted marker.
        return System.Text.RegularExpressions.Regex.Replace(
            message,
            @"\[Sensitive\][^\s,}]*",
            _ => _redactor.Redact(string.Empty));
    }
}
