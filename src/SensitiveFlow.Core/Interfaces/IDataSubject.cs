namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Contract that an entity can implement to expose the data subject ID.
/// </summary>
public interface IDataSubject
{
    /// <summary>Identifier of the data subject represented by the entity.</summary>
    string DataSubjectId { get; }
}
