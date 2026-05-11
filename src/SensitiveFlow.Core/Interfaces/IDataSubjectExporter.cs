namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Exports data-subject data from a specific source type.
/// </summary>
/// <typeparam name="T">Source type to export.</typeparam>
public interface IDataSubjectExporter<in T>
{
    /// <summary>Exports data from the given source object.</summary>
    Task<IReadOnlyDictionary<string, object?>> ExportAsync(T source, CancellationToken cancellationToken = default);
}

