namespace SensitiveFlow.Anonymization.Export;

/// <summary>
/// Extracts every property annotated with <c>[PersonalData]</c>, <c>[SensitiveData]</c>, or
/// <c>[RetentionData]</c> from an entity into a portable dictionary. Useful for satisfying
/// data-portability requests where a data subject asks for a copy of the personal data
/// the application holds about them.
/// </summary>
/// <remarks>
/// The exporter operates on individual entity instances. Building the cross-table view of
/// "everything we know about this subject" is the application's responsibility — typically
/// by querying each table for rows where the subject identifier matches and feeding each
/// row through <see cref="Export"/>.
/// </remarks>
public interface IDataSubjectExporter
{
    /// <summary>
    /// Returns a dictionary keyed by property name with the current value of every annotated
    /// property of <paramref name="entity"/>. Properties with non-readable getters are skipped.
    /// </summary>
    IReadOnlyDictionary<string, object?> Export(object entity);
}
