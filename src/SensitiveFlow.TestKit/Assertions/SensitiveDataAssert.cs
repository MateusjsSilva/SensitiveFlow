using SensitiveFlow.Core.Reflection;
using Xunit.Sdk;

namespace SensitiveFlow.TestKit.Assertions;

/// <summary>
/// Test-time assertions that catch redaction regressions: a property annotated with
/// <c>[PersonalData]</c> or <c>[SensitiveData]</c> appearing in clear text inside a string
/// (typically a serialized payload, a log line, or an exception message) is a leak.
/// </summary>
public static class SensitiveDataAssert
{
    /// <summary>
    /// Fails the test if any sensitive property value of <paramref name="entity"/> appears
    /// verbatim inside <paramref name="payload"/>. An empty or null property value is ignored
    /// (an empty substring would always match).
    /// </summary>
    /// <param name="payload">The string suspected of leaking — usually a serialized JSON, log line, or HTTP response body.</param>
    /// <param name="entity">The entity whose annotated properties should NOT appear in <paramref name="payload"/>.</param>
    public static void DoesNotLeak(string payload, object entity)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(entity);

        var leaks = new List<string>();
        var properties = SensitiveMemberCache.GetSensitiveProperties(entity.GetType());

        foreach (var property in properties)
        {
            if (!property.CanRead)
            {
                continue;
            }

            if (property.GetValue(entity) is not string value || string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (payload.Contains(value, StringComparison.Ordinal))
            {
                leaks.Add($"{property.DeclaringType?.Name}.{property.Name} = \"{value}\"");
            }
        }

        if (leaks.Count > 0)
        {
            throw new XunitException(
                "Sensitive values appeared in payload (redaction regression):"
                + Environment.NewLine
                + string.Join(Environment.NewLine, leaks.Select(l => "  - " + l)));
        }
    }

    /// <summary>
    /// Convenience overload that checks a payload against multiple entities at once
    /// (e.g. the request DTO and the loaded domain entity).
    /// </summary>
    public static void DoesNotLeak(string payload, params object[] entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        foreach (var entity in entities)
        {
            if (entity is null)
            {
                continue;
            }
            DoesNotLeak(payload, entity);
        }
    }
}
