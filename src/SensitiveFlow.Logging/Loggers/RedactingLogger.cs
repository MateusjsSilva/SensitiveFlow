using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Diagnostics;
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
    private const string OriginalFormatKey = "{OriginalFormat}";

    private static readonly Regex SensitiveKeyPattern =
        new(@"^\[Sensitive\]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex SensitiveTemplatePattern =
        new(@"\[Sensitive\][^\s,}]*", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    // Matches {Name}, {Name:format} and {Name,align} placeholders. The name is
    // captured to look up the resolved value from the structured state.
    private static readonly Regex PlaceholderPattern =
        new(@"\{(?<name>[^{}:,]+)(?:[:,][^{}]*)?\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Meter Meter = new(SensitiveFlowDiagnostics.MeterName);

    private static readonly Counter<long> RedactedFieldsCounter = Meter.CreateCounter<long>(
        name: SensitiveFlowDiagnostics.RedactFieldsCountName,
        unit: "fields",
        description: "Sensitive fields replaced before reaching a log sink.");

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
            _inner.Log(logLevel, eventId, redacted, exception, (s, ex) =>
            {
                // Prefer template-driven rendering: rebuild the message from {OriginalFormat}
                // using the already-redacted values. This avoids substring corruption that
                // a global string Replace would cause when a sensitive value happens to
                // appear inside another field.
                var template = FindOriginalFormat(s);
                if (template is not null)
                {
                    return RenderTemplate(template, s);
                }

                // No template available — render with the original formatter and redact
                // any [Sensitive]<token> patterns left in the output.
                var message = formatter(state, ex);
                return RedactTemplate(message);
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
    {
        // Fast-path: skip regex entirely when there are no [Sensitive] markers in the string.
        if (!message.Contains("[Sensitive]", StringComparison.Ordinal))
        {
            return message;
        }

        return SensitiveTemplatePattern.Replace(message, _ => _redactor.Redact(string.Empty));
    }

    private static string? FindOriginalFormat(IEnumerable<KeyValuePair<string, object?>> pairs)
    {
        foreach (var pair in pairs)
        {
            if (pair.Key == OriginalFormatKey && pair.Value is string template)
            {
                return template;
            }
        }
        return null;
    }

    private static string RenderTemplate(string template, IEnumerable<KeyValuePair<string, object?>> pairs)
        => PlaceholderPattern.Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            foreach (var pair in pairs)
            {
                if (pair.Key == name)
                {
                    return pair.Value?.ToString() ?? string.Empty;
                }
            }
            return m.Value;
        });

    private List<KeyValuePair<string, object?>> RedactPairs(IEnumerable<KeyValuePair<string, object?>> pairs)
    {
        var result = new List<KeyValuePair<string, object?>>();
        var redactedCount = 0;
        foreach (var pair in pairs)
        {
            if (SensitiveKeyPattern.IsMatch(pair.Key))
            {
                result.Add(new KeyValuePair<string, object?>(pair.Key, _redactor.Redact(string.Empty)));
                redactedCount++;
            }
            else
            {
                result.Add(pair);
            }
        }

        if (redactedCount > 0)
        {
            RedactedFieldsCounter.Add(redactedCount);
        }

        return result;
    }
}
