using System.Reflection;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Profiles;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.Anonymization.Export;

/// <summary>
/// Default <see cref="IDataSubjectExporter"/>. Reuses <see cref="SensitiveMemberCache"/> so
/// repeated exports across many entities of the same type avoid re-scanning attributes.
/// </summary>
public sealed class DataSubjectExporter : IDataSubjectExporter
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Export(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var type = entity.GetType();
        var sensitive = SensitiveMemberCache.GetSensitiveProperties(type);
        var retention = SensitiveMemberCache.GetRetentionProperties(type);

        // Use a dictionary keyed by property name. A retention-only property (annotated with
        // [RetentionData] but neither [PersonalData] nor [SensitiveData]) should still be
        // exported because it carries data the user asked for.
        var result = new Dictionary<string, object?>(sensitive.Count + retention.Count, StringComparer.Ordinal);

        foreach (var property in sensitive)
        {
            if (!property.CanRead)
            {
                continue;
            }

            AddExportValue(result, entity, property);
        }

        foreach (var entry in retention)
        {
            if (!entry.Property.CanRead || result.ContainsKey(entry.Property.Name))
            {
                continue;
            }

            AddExportValue(result, entity, entry.Property);
        }

        return result;
    }

    private static void AddExportValue(Dictionary<string, object?> result, object entity, PropertyInfo property)
    {
        var action = property.GetCustomAttribute<RedactionAttribute>(inherit: true)
            ?.ForContext(RedactionContext.Export)
            ?? OutputRedactionAction.None;

        if (action == OutputRedactionAction.Omit)
        {
            return;
        }

        var value = property.GetValue(entity);
        result[property.Name] = action switch
        {
            OutputRedactionAction.Redact => SensitiveFlowDefaults.RedactedPlaceholder,
            OutputRedactionAction.Mask => MaskValue(value, property),
            _ => value,
        };
    }

    private static object? MaskValue(object? value, PropertyInfo property)
    {
        if (value is not string text)
        {
            return null;
        }

        if (text.Length == 0)
        {
            return text;
        }

        var kind = property.GetCustomAttribute<MaskKindAttribute>(inherit: true)?.Kind
            ?? InferMaskKind(property.Name);
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
        => string.Join(" ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(GenericMask));

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
