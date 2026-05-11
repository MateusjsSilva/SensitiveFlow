using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Reflection;
using SensitiveFlow.Logging.Configuration;
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
    private readonly SensitiveLoggingOptions _options;

    /// <summary>Initializes a new instance of <see cref="RedactingLogger"/>.</summary>
    public RedactingLogger(
        ILogger inner,
        ISensitiveValueRedactor redactor,
        SensitiveLoggingOptions? options = null)
    {
        _inner = inner;
        _redactor = redactor;
        _options = options ?? new SensitiveLoggingOptions();
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
                if (s is TState typedState)
                {
                    return RedactTemplate(formatter(typedState, ex));
                }

                return RenderPairsFallback(s);
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

    private static string RenderPairsFallback(IEnumerable<KeyValuePair<string, object?>> pairs)
        => string.Join(", ", pairs
            .Where(static pair => pair.Key != OriginalFormatKey)
            .Select(static pair => $"{pair.Key}={pair.Value}"));

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
            else if (_options.RedactAnnotatedObjects && TryRedactAnnotatedObject(pair.Value, out var redactedValue, out var objectRedactedCount))
            {
                result.Add(new KeyValuePair<string, object?>(pair.Key, redactedValue));
                redactedCount += objectRedactedCount;
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

    private bool TryRedactAnnotatedObject(object? value, out object? redactedValue, out int redactedCount)
    {
        redactedValue = value;
        redactedCount = 0;

        if (value is null || value is string || value.GetType().IsValueType)
        {
            return false;
        }

        var type = value.GetType();
        var sensitiveProperties = SensitiveMemberCache.GetSensitiveProperties(type);
        if (sensitiveProperties.Count == 0)
        {
            return false;
        }

        var sensitive = sensitiveProperties.ToDictionary(static p => p.Name, StringComparer.Ordinal);
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            if (!sensitive.TryGetValue(property.Name, out var sensitiveProperty))
            {
                projected[property.Name] = property.GetValue(value);
                continue;
            }

            var action = ResolveLogAction(sensitiveProperty);
            if (action == OutputRedactionAction.None)
            {
                projected[property.Name] = property.GetValue(value);
                continue;
            }

            if (action == OutputRedactionAction.Omit)
            {
                redactedCount++;
                continue;
            }

            var raw = property.GetValue(value);
            projected[property.Name] = action == OutputRedactionAction.Mask
                ? MaskValue(raw, sensitiveProperty)
                : _redactor.Redact(raw?.ToString() ?? string.Empty);
            redactedCount++;
        }

        redactedValue = projected;
        return redactedCount > 0;
    }

    private OutputRedactionAction ResolveLogAction(PropertyInfo property)
    {
        if (property.GetCustomAttribute<AllowSensitiveLoggingAttribute>(inherit: true) is not null)
        {
            return OutputRedactionAction.None;
        }

        var contextual = property.GetCustomAttribute<RedactionAttribute>(inherit: true);
        var contextualAction = contextual?.ForContext(RedactionContext.Log) ?? OutputRedactionAction.None;
        if (contextualAction != OutputRedactionAction.None)
        {
            return contextualAction;
        }

        if (property.GetCustomAttribute<OmitAttribute>(inherit: true) is not null)
        {
            return OutputRedactionAction.Omit;
        }

        if (property.GetCustomAttribute<RedactAttribute>(inherit: true) is not null)
        {
            return OutputRedactionAction.Redact;
        }

        if (property.GetCustomAttribute<MaskAttribute>(inherit: true) is not null)
        {
            return OutputRedactionAction.Mask;
        }

        var policyAction = ResolvePolicyAction(property);
        if (policyAction != OutputRedactionAction.None)
        {
            return policyAction;
        }

        return _options.DefaultAnnotatedMemberAction;
    }

    private OutputRedactionAction ResolvePolicyAction(PropertyInfo property)
    {
        var policies = _options.Policies;
        if (policies is null)
        {
            return OutputRedactionAction.None;
        }

        var personal = property.GetCustomAttribute<PersonalDataAttribute>(inherit: true);
        var personalRule = personal is null ? null : policies.Find(personal.Category);
        if (HasMaskInLogs(personalRule))
        {
            return OutputRedactionAction.Mask;
        }

        var sensitive = property.GetCustomAttribute<SensitiveDataAttribute>(inherit: true);
        var sensitiveRule = sensitive is null ? null : policies.Find(sensitive.Category);
        return HasMaskInLogs(sensitiveRule) ? OutputRedactionAction.Mask : OutputRedactionAction.None;
    }

    private static bool HasMaskInLogs(SensitiveFlowPolicyRule? rule)
        => rule is not null
        && (rule.Actions & SensitiveFlowPolicyAction.MaskInLogs) == SensitiveFlowPolicyAction.MaskInLogs;

    private object? MaskValue(object? value, PropertyInfo property)
    {
        if (value is not string text)
        {
            return _redactor.Redact(value?.ToString() ?? string.Empty);
        }

        if (text.Length == 0)
        {
            return text;
        }

        var mask = property.GetCustomAttribute<MaskAttribute>(inherit: true);
        var kind = mask?.Kind ?? InferMaskKind(property.Name);
        return kind switch
        {
            MaskKind.Email => MaskEmail(text),
            MaskKind.Phone => MaskPhone(text),
            MaskKind.Name => MaskName(text),
            _ => GenericMask(text),
        };
    }

    private static MaskKind InferMaskKind(string propertyName)
    {
        if (propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return MaskKind.Email;
        }

        if (propertyName.Contains("Phone", StringComparison.OrdinalIgnoreCase))
        {
            return MaskKind.Phone;
        }

        if (propertyName.Contains("Name", StringComparison.OrdinalIgnoreCase))
        {
            return MaskKind.Name;
        }

        return MaskKind.Generic;
    }

    private static string MaskEmail(string value)
    {
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 1)
        {
            return GenericMask(value);
        }

        return value[0] + new string('*', at - 1) + value[at..];
    }

    private static string MaskPhone(string value)
    {
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

    private static string MaskName(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return value;
        }

        return string.Join(" ", parts.Select(GenericMask));
    }

    private static string GenericMask(string value)
    {
        if (value.Length == 1)
        {
            return "*";
        }

        return string.Create(value.Length, value, static (span, source) =>
        {
            span[0] = source[0];
            for (var i = 1; i < span.Length; i++)
            {
                span[i] = '*';
            }
        });
    }
}
