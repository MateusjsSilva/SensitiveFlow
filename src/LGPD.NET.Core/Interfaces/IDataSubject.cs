namespace LGPD.NET.Core.Interfaces;

/// <summary>
/// Contract that an entity can implement to expose the data subject ID.
/// </summary>
public interface IDataSubject
{
    string DataSubjectId { get; }
}
