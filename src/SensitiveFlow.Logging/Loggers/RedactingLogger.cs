using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Logging.Redaction;

namespace SensitiveFlow.Logging.Loggers;

/// <summary>
/// <see cref="ILogger"/> decorator that redacts sensitive structured log values before
/// forwarding to the inner logger.
/// <para>
/// Mark sensitive structured log parameters with the <c>[Sensitive]</c> prefix:
/// <code>
/// logger.LogInformation("User {[Sensitive]Email} logged in", email);
/// </code>
/// The redactor replaces both the rendered message text <b>and</b> the structured
/// property value in the <c>TState</c> key-value pairs, so sinks that consume
/// structured properties (Serilog, seq, OpenTelemetry) never receive the raw value.
/// </para>
/// </summary>
public sealed class RedactingLogger : ILogger
{
    private static readonly Regex SensitiveKeyPattern =
        new(@"^\[Sensitive\]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex SensitiveTemplatePattern =
        new(@"\[Sensitive\][^\s,}]*", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

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
        {
            return;
        }

        // When TState carries structured key-value pairs (the common case with
        // Microsoft.Extensions.Logging message templates), redact values whose key
        // starts with [Sensitive] so sinks that consume structured properties
        // (Serilog, seq, OpenTelemetry) never receive the raw value.
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            var redacted = RedactPairs(pairs);
            _inner.Log(logLevel, eventId, redacted, exception, (_, ex) =>
            {
                var message = formatter(state, ex);
                return RedactSensitiveValues(message, pairs);
            });
            return;
        }

        _inner.Log(logLevel, eventId, state, exception, (s, ex) =>
        {
            var message = formatter(s, ex);
            return RedactTemplate(message);
        });
    }

    // Visible for testing.
    internal string RedactTemplate(string message)
        => SensitiveTemplatePattern.Replace(message, _ => _redactor.Redact(string.Empty));

    private string RedactSensitiveValues(string message, IEnumerable<KeyValuePair<string, object?>> pairs)
    {
        var redacted = RedactTemplate(message);
        foreach (var pair in pairs)
        {
            if (!SensitiveKeyPattern.IsMatch(pair.Key) || pair.Value is null)
            {
                continue;
            }

            var value = pair.Value.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                redacted = redacted.Replace(value, _redactor.Redact(string.Empty), StringComparison.Ordinal);
            }
        }

        return redacted;
    }

    private List<KeyValuePair<string, object?>> RedactPairs(IEnumerable<KeyValuePair<string, object?>> pairs)
    {
        var result = new List<KeyValuePair<string, object?>>();
        foreach (var pair in pairs)
        {
            if (SensitiveKeyPattern.IsMatch(pair.Key))
            {
                result.Add(new KeyValuePair<string, object?>(pair.Key, _redactor.Redact(string.Empty)));
            }
            else
            {
                result.Add(pair);
            }
        }

        return result;
    }
}
