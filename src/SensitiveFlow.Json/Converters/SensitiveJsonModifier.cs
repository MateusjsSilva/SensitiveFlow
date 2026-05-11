using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Core.Reflection;
using SensitiveFlow.Json.Attributes;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Enums;

namespace SensitiveFlow.Json.Converters;

/// <summary>
/// <see cref="IJsonTypeInfoResolver"/> modifier that rewrites <see cref="JsonPropertyInfo"/>
/// for properties annotated with <c>[PersonalData]</c> or <c>[SensitiveData]</c> so that
/// the value is masked, replaced, or omitted at serialization time.
/// </summary>
/// <remarks>
/// Use via <c>JsonRedactionExtensions.WithSensitiveDataRedaction</c> on a
/// <see cref="JsonSerializerOptions"/> instance.
/// </remarks>
public static class SensitiveJsonModifier
{
    /// <summary>
    /// Returns a <see cref="JsonTypeInfo"/> modifier action that applies the given
    /// <paramref name="options"/> to every relevant property in every type seen.
    /// </summary>
    public static Action<JsonTypeInfo> Create(JsonRedactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return typeInfo =>
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            var sensitiveProperties = SensitiveMemberCache.GetSensitiveProperties(typeInfo.Type);
            if (sensitiveProperties.Count == 0)
            {
                return;
            }

            var sensitiveLookup = sensitiveProperties.ToDictionary(p => p.Name, StringComparer.Ordinal);

            for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                var jsonProperty = typeInfo.Properties[i];
                if (jsonProperty.AttributeProvider is not PropertyInfo clrProperty)
                {
                    continue;
                }

                if (!sensitiveLookup.ContainsKey(clrProperty.Name))
                {
                    continue;
                }

                var mode = ResolveMode(clrProperty, options);

                if (mode == JsonRedactionMode.None)
                {
                    continue;
                }

                if (mode == JsonRedactionMode.Omit)
                {
                    typeInfo.Properties.RemoveAt(i);
                    continue;
                }

                ApplyRedactingGetter(jsonProperty, mode, options.RedactedPlaceholder);
            }
        };
    }

    private static JsonRedactionMode ResolveMode(PropertyInfo property, JsonRedactionOptions options)
    {
        var overrideAttr = property.GetCustomAttribute<JsonRedactionAttribute>(inherit: true);
        if (overrideAttr is not null)
        {
            return overrideAttr.Mode;
        }

        var contextual = property.GetCustomAttribute<RedactionAttribute>(inherit: true);
        var contextualAction = contextual?.ForContext(Core.Enums.RedactionContext.ApiResponse)
            ?? Core.Enums.OutputRedactionAction.None;
        if (contextualAction != Core.Enums.OutputRedactionAction.None)
        {
            return ToJsonMode(contextualAction);
        }

        if (property.GetCustomAttribute<OmitAttribute>(inherit: true) is not null)
        {
            return JsonRedactionMode.Omit;
        }

        if (property.GetCustomAttribute<RedactAttribute>(inherit: true) is not null)
        {
            return JsonRedactionMode.Redacted;
        }

        if (property.GetCustomAttribute<MaskAttribute>(inherit: true) is not null)
        {
            return JsonRedactionMode.Mask;
        }

        var policyMode = ResolvePolicyMode(property, options.Policies);
        return policyMode ?? options.DefaultMode;
    }

    private static JsonRedactionMode? ResolvePolicyMode(PropertyInfo property, SensitiveFlowPolicyRegistry? policies)
    {
        if (policies is null)
        {
            return null;
        }

        var personal = property.GetCustomAttribute<PersonalDataAttribute>(inherit: true);
        var personalRule = personal is null ? null : policies.Find(personal.Category);
        if (personalRule is not null)
        {
            return ToJsonMode(personalRule.Actions);
        }

        var sensitive = property.GetCustomAttribute<SensitiveDataAttribute>(inherit: true);
        var sensitiveRule = sensitive is null ? null : policies.Find(sensitive.Category);
        return sensitiveRule is null ? null : ToJsonMode(sensitiveRule.Actions);
    }

    private static JsonRedactionMode? ToJsonMode(SensitiveFlowPolicyAction actions)
    {
        if ((actions & SensitiveFlowPolicyAction.OmitInJson) == SensitiveFlowPolicyAction.OmitInJson)
        {
            return JsonRedactionMode.Omit;
        }

        if ((actions & SensitiveFlowPolicyAction.RedactInJson) == SensitiveFlowPolicyAction.RedactInJson)
        {
            return JsonRedactionMode.Redacted;
        }

        return null;
    }

    private static JsonRedactionMode ToJsonMode(Core.Enums.OutputRedactionAction action)
    {
        return action switch
        {
            Core.Enums.OutputRedactionAction.Omit => JsonRedactionMode.Omit,
            Core.Enums.OutputRedactionAction.Redact => JsonRedactionMode.Redacted,
            Core.Enums.OutputRedactionAction.Mask => JsonRedactionMode.Mask,
            Core.Enums.OutputRedactionAction.None => JsonRedactionMode.None,
            _ => JsonRedactionMode.Redacted,
        };
    }

    private static void ApplyRedactingGetter(JsonPropertyInfo jsonProperty, JsonRedactionMode mode, string placeholder)
    {
        var originalGetter = jsonProperty.Get;
        if (originalGetter is null)
        {
            return;
        }

        var propertyName = jsonProperty.Name;

        jsonProperty.Get = obj =>
        {
            var value = originalGetter(obj);
            return Redact(value, mode, placeholder, propertyName, jsonProperty.PropertyType);
        };
    }

    private static object? Redact(
        object? value,
        JsonRedactionMode mode,
        string placeholder,
        string propertyName,
        Type propertyType)
    {
        if (value is null)
        {
            return null;
        }

        if (propertyType != typeof(string))
        {
            return propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null;
        }

        return mode switch
        {
            JsonRedactionMode.Redacted => placeholder,
            JsonRedactionMode.Mask => MaskValue(value, placeholder, propertyName),
            _ => value,
        };
    }

    private static object MaskValue(object value, string placeholder, string propertyName)
    {
        if (value is not string s)
        {
            return placeholder;
        }

        if (s.Length == 0)
        {
            return s;
        }

        // Heuristics by property name. These match the existing maskers in
        // SensitiveFlow.Anonymization. For anything we don't recognize, fall back to
        // a generic "first char + asterisks" mask that preserves length but not value.
        if (propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return s.MaskEmail();
        }

        if (propertyName.Contains("Phone", StringComparison.OrdinalIgnoreCase))
        {
            return s.MaskPhone();
        }

        if (propertyName.Contains("Name", StringComparison.OrdinalIgnoreCase))
        {
            return s.MaskName();
        }

        return GenericMask(s);
    }

    private static string GenericMask(string s)
    {
        if (s.Length == 1)
        {
            return "*";
        }

        return string.Create(s.Length, s, static (span, source) =>
        {
            span[0] = source[0];
            for (var i = 1; i < span.Length; i++)
            {
                span[i] = '*';
            }
        });
    }
}
