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

            result[property.Name] = property.GetValue(entity);
        }

        foreach (var entry in retention)
        {
            if (!entry.Property.CanRead || result.ContainsKey(entry.Property.Name))
            {
                continue;
            }

            result[entry.Property.Name] = entry.Property.GetValue(entity);
        }

        return result;
    }
}
