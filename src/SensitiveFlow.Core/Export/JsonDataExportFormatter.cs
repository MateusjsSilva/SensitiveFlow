using System.Text.Json;
using System.Text.Json.Serialization;

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
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(rows, options);
    }
}

