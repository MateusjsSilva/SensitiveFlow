using System.Text;
using System.Text.Json;

namespace SensitiveFlow.Core.Discovery;

/// <summary>
/// Report produced by scanning assemblies for SensitiveFlow annotations.
/// </summary>
public sealed class SensitiveDataDiscoveryReport
{
    /// <summary>Initializes a new report.</summary>
    public SensitiveDataDiscoveryReport(IEnumerable<SensitiveDataDiscoveryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries.OrderBy(static e => e.TypeName, StringComparer.Ordinal)
            .ThenBy(static e => e.MemberName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Gets discovered entries.</summary>
    public IReadOnlyList<SensitiveDataDiscoveryEntry> Entries { get; }

    /// <summary>Serializes the report to indented JSON.</summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(Entries, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Renders the report as a Markdown table.</summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Member | Annotation | Category | Sensitivity | Retention |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var entry in Entries)
        {
            var category = entry.Category?.ToString()
                ?? entry.SensitiveCategory?.ToString()
                ?? "Other";
            var retention = entry.RetentionPolicy is null
                ? string.Empty
                : $"{entry.RetentionYears.GetValueOrDefault()}y {entry.RetentionMonths.GetValueOrDefault()}m / {entry.RetentionPolicy}";

            sb.AppendLine(CultureInvariant(
                $"| {entry.TypeName}.{entry.MemberName} | {entry.Annotation} | {category} | {entry.Sensitivity} | {retention} |"));
        }

        return sb.ToString();
    }

    private static string CultureInvariant(FormattableString value)
    {
        return FormattableString.Invariant(value);
    }
}

