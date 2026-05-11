using System.Text.Json;

namespace SensitiveFlow.Core.Export;

/// <summary>
/// JSON formatter for data export rows.
/// </summary>
public sealed class JsonDataExportFormatter : IDataExportFormatter
{
    /// <inheritdoc />
    public string Format(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
    }
}

