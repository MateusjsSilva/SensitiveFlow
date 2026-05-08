namespace SensitiveFlow.Anonymization.Erasure;

/// <summary>
/// Applies a "right to be forgotten" erasure pass over an entity, transforming every
/// property annotated with <c>[PersonalData]</c>, <c>[SensitiveData]</c>, or <c>[RetentionData]</c>
/// according to a registered <see cref="IErasureStrategy"/>.
/// </summary>
/// <remarks>
/// The service does <b>not</b> persist changes — it mutates the in-memory entity. The caller is
/// responsible for calling <c>SaveChanges</c> (or equivalent) and for emitting the corresponding
/// audit record (see <c>AuditOperation.Anonymize</c>).
/// </remarks>
public interface IDataSubjectErasureService
{
    /// <summary>
    /// Walks the annotated string properties of <paramref name="entity"/> and overwrites each
    /// using the configured erasure strategy.
    /// </summary>
    /// <param name="entity">The entity instance to erase.</param>
    /// <returns>The number of properties that were overwritten.</returns>
    int Erase(object entity);
}
