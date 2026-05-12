using System.Reflection;
using SensitiveFlow.Core.Attributes;

namespace SensitiveFlow.Core.Discovery;

/// <summary>
/// Scans assemblies for SensitiveFlow annotations and produces in-memory, JSON, or Markdown reports.
/// </summary>
public static class SensitiveDataDiscovery
{
    /// <summary>Scans one assembly for annotated fields and properties.</summary>
    public static SensitiveDataDiscoveryReport Scan(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return Scan([assembly]);
    }

    /// <summary>Scans assemblies for annotated fields and properties.</summary>
    public static SensitiveDataDiscoveryReport Scan(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var entries = new List<SensitiveDataDiscoveryEntry>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(static t => !t.IsGenericTypeDefinition))
            {
                foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (member.MemberType is not (MemberTypes.Property or MemberTypes.Field))
                    {
                        continue;
                    }

                    var personal = member.GetCustomAttribute<PersonalDataAttribute>(inherit: true);
                    var sensitive = member.GetCustomAttribute<SensitiveDataAttribute>(inherit: true);
                    var retention = member.GetCustomAttribute<RetentionDataAttribute>(inherit: true);

                    if (personal is null && sensitive is null && retention is null)
                    {
                        continue;
                    }

                    entries.Add(new SensitiveDataDiscoveryEntry
                    {
                        TypeName = type.Name,
                        MemberName = member.Name,
                        Annotation = sensitive is not null
                            ? nameof(SensitiveDataAttribute)
                            : personal is not null ? nameof(PersonalDataAttribute) : nameof(RetentionDataAttribute),
                        Category = personal?.Category,
                        SensitiveCategory = sensitive?.Category,
                        Sensitivity = sensitive?.Sensitivity ?? personal?.Sensitivity ?? Core.Enums.DataSensitivity.Medium,
                        RetentionYears = retention?.Years,
                        RetentionMonths = retention?.Months,
                        RetentionPolicy = retention?.Policy,
                    });
                }
            }
        }

        return new SensitiveDataDiscoveryReport(entries);
    }
}
