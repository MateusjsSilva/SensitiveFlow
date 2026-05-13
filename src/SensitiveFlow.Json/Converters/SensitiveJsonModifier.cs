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

                var redactionSettings = ResolveSettings(clrProperty, options);
                var mode = redactionSettings.Mode;

                if (mode == JsonRedactionMode.None)
                {
                    continue;
                }

                if (mode == JsonRedactionMode.Omit)
                {
                    typeInfo.Properties.RemoveAt(i);
                    continue;
                }

                if (jsonProperty.PropertyType != typeof(string)
                    && options.NonStringRedactionMode == JsonNonStringRedactionMode.Omit)
                {
                    typeInfo.Properties.RemoveAt(i);
                    continue;
                }

                if (jsonProperty.PropertyType == typeof(string))
                {
                    ApplyRedactingGetter(jsonProperty, redactionSettings, options.RedactedPlaceholder);
                    continue;
                }

                ReplaceWithRedactingProperty(
                    typeInfo,
                    i,
                    jsonProperty,
                    clrProperty,
                    redactionSettings,
                    options.RedactedPlaceholder,
                    options.NonStringRedactionMode);
            }
        };
    }

    private static JsonRedactionSettings ResolveSettings(PropertyInfo property, JsonRedactionOptions options)
    {
        var overrideAttr = property.GetCustomAttribute<JsonRedactionAttribute>(inherit: true);
        return new JsonRedactionSettings(
            ResolveMode(property, options, overrideAttr),
            overrideAttr is { HasPreservePrefixLength: true }
                ? overrideAttr.PreservePrefixLength
                : null);
    }

    private static JsonRedactionMode ResolveMode(
        PropertyInfo property,
        JsonRedactionOptions options,
        JsonRedactionAttribute? overrideAttr)
    {
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

    private static void ApplyRedactingGetter(
        JsonPropertyInfo jsonProperty,
        JsonRedactionSettings settings,
        string placeholder)
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
            return Redact(value, settings, placeholder, propertyName, jsonProperty.PropertyType);
        };
    }

    private static void ReplaceWithRedactingProperty(
        JsonTypeInfo typeInfo,
        int index,
        JsonPropertyInfo originalProperty,
        PropertyInfo clrProperty,
        JsonRedactionSettings settings,
        string placeholder,
        JsonNonStringRedactionMode nonStringMode)
    {
        var originalGetter = originalProperty.Get;
        if (originalGetter is null)
        {
            return;
        }

        var replacementType = nonStringMode == JsonNonStringRedactionMode.Null
            ? GetNullableJsonPropertyType(originalProperty.PropertyType)
            : IsCollectionType(originalProperty.PropertyType)
            ? typeof(string[])
            : typeof(string);

        var replacement = typeInfo.CreateJsonPropertyInfo(replacementType, originalProperty.Name);
        replacement.AttributeProvider = originalProperty.AttributeProvider;
        replacement.Get = obj =>
        {
            var value = originalGetter(obj);
            return Redact(value, settings, placeholder, originalProperty.Name, clrProperty.PropertyType, nonStringMode);
        };

        typeInfo.Properties.RemoveAt(index);
        typeInfo.Properties.Insert(index, replacement);
    }

    private static object? Redact(
        object? value,
        JsonRedactionSettings settings,
        string placeholder,
        string propertyName,
        Type propertyType,
        JsonNonStringRedactionMode nonStringMode = JsonNonStringRedactionMode.Placeholder)
    {
        if (value is null)
        {
            return null;
        }

        if (propertyType != typeof(string) && nonStringMode == JsonNonStringRedactionMode.Null)
        {
            return null;
        }

        if (IsCollectionType(propertyType))
        {
            return settings.Mode switch
            {
                JsonRedactionMode.Redacted => RedactCollection((System.Collections.IEnumerable)value, placeholder),
                JsonRedactionMode.Mask => MaskCollection((System.Collections.IEnumerable)value, placeholder, propertyName, settings.PreservePrefixLength),
                _ => RedactCollection((System.Collections.IEnumerable)value, placeholder),
            };
        }

        if (propertyType == typeof(string) && value is not string)
        {
            return placeholder;
        }

        if (!IsScalarType(propertyType) && settings.Mode == JsonRedactionMode.Redacted)
        {
            return null;
        }

        if (propertyType != typeof(string)
            && settings.Mode is not JsonRedactionMode.Redacted
            && settings.Mode is not JsonRedactionMode.Mask)
        {
            return placeholder;
        }

        return settings.Mode switch
        {
            JsonRedactionMode.Redacted => placeholder,
            JsonRedactionMode.Mask => MaskValue(value, placeholder, propertyName, settings.PreservePrefixLength),
            _ => value,
        };
    }

    private static object MaskValue(
        object value,
        string placeholder,
        string propertyName,
        int? preservePrefixLength)
    {
        if (value is not string s)
        {
            if (value is System.Collections.IEnumerable enumerable)
            {
                return MaskCollection(enumerable, placeholder, propertyName, preservePrefixLength);
            }

            return MaskScalar(value, placeholder);
        }

        if (s.Length == 0)
        {
            return s;
        }

        if (preservePrefixLength is not null)
        {
            return GenericMask(s, preservePrefixLength.Value);
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

    private static string[] MaskCollection(
        System.Collections.IEnumerable enumerable,
        string placeholder,
        string propertyName,
        int? preservePrefixLength)
    {
        var values = new List<string>();
        foreach (var item in enumerable)
        {
            values.Add(item is string s
                ? (string)MaskValue(s, placeholder, propertyName, preservePrefixLength)
                : MaskScalar(item, placeholder));
        }

        return values.ToArray();
    }

    private static string[] RedactCollection(System.Collections.IEnumerable enumerable, string placeholder)
    {
        var values = new List<string>();
        foreach (var _ in enumerable)
        {
            values.Add(placeholder);
        }

        return values.ToArray();
    }

    private static string MaskScalar(object? value, string placeholder)
    {
        if (value is null)
        {
            return placeholder;
        }

        return value switch
        {
            DateTime => "[DATE_REDACTED]",
            DateTimeOffset => "[DATE_REDACTED]",
            TimeOnly => "[TIME_REDACTED]",
            DateOnly => "[DATE_REDACTED]",
            bool => "[BOOLEAN_REDACTED]",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "[NUMBER_REDACTED]",
            _ => placeholder,
        };
    }

    private static bool IsCollectionType(Type type)
        => type != typeof(string)
           && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    private static Type GetNullableJsonPropertyType(Type type)
    {
        if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
        {
            return type;
        }

        return typeof(Nullable<>).MakeGenericType(type);
    }

    private static bool IsScalarType(Type type)
    {
        var nullableType = Nullable.GetUnderlyingType(type) ?? type;

        return nullableType.IsEnum
               || nullableType == typeof(string)
               || nullableType == typeof(DateTime)
               || nullableType == typeof(DateTimeOffset)
               || nullableType == typeof(TimeOnly)
               || nullableType == typeof(DateOnly)
               || nullableType == typeof(bool)
               || nullableType == typeof(byte)
               || nullableType == typeof(sbyte)
               || nullableType == typeof(short)
               || nullableType == typeof(ushort)
               || nullableType == typeof(int)
               || nullableType == typeof(uint)
               || nullableType == typeof(long)
               || nullableType == typeof(ulong)
               || nullableType == typeof(float)
               || nullableType == typeof(double)
               || nullableType == typeof(decimal);
    }

    private static string GenericMask(string s, int preservePrefixLength = 1)
    {
        if (s.Length == 1)
        {
            return "*";
        }

        var visibleCharacters = Math.Clamp(preservePrefixLength, 0, s.Length);
        return string.Create(s.Length, (s, visibleCharacters), static (span, state) =>
        {
            for (var i = 0; i < state.visibleCharacters; i++)
            {
                span[i] = state.s[i];
            }

            for (var i = state.visibleCharacters; i < span.Length; i++)
            {
                span[i] = '*';
            }
        });
    }

    private readonly record struct JsonRedactionSettings(
        JsonRedactionMode Mode,
        int? PreservePrefixLength);
}
