namespace SensitiveFlow.Core.Export;

/// <summary>
/// Formats data export rows into a transport representation.
/// </summary>
public interface IDataExportFormatter
{
    /// <summary>Formats the given rows.</summary>
    string Format(IEnumerable<IReadOnlyDictionary<string, object?>> rows);
}

