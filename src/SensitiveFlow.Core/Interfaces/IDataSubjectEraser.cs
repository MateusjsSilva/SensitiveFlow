namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Erases or anonymizes data-subject data in a specific source type.
/// </summary>
/// <typeparam name="T">Source type to erase.</typeparam>
public interface IDataSubjectEraser<in T>
{
    /// <summary>Erases or anonymizes data for the given source object.</summary>
    Task EraseAsync(T source, CancellationToken cancellationToken = default);
}

