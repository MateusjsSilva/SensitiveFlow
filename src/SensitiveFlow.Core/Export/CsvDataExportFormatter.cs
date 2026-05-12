using System.Text;

namespace SensitiveFlow.Core.Export;

/// <summary>
/// CSV formatter with formula-injection protection for spreadsheet consumers.
/// </summary>
public sealed class CsvDataExportFormatter : IDataExportFormatter
{
    /// <inheritdoc />
    public string Format(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var materialized = rows.ToArray();
        if (materialized.Length == 0)
        {
            return string.Empty;
        }

        var headers = materialized.SelectMany(static row => row.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in materialized)
        {
            sb.AppendLine(string.Join(",", headers.Select(h => Escape(row.TryGetValue(h, out var value) ? value?.ToString() : string.Empty))));
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && "=+-@".Contains(safe[0], StringComparison.Ordinal))
        {
            safe = "'" + safe;
        }

        return "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
