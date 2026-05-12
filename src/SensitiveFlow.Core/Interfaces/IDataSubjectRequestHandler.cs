namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Handles a data-subject request coordinated by the application.
/// </summary>
public interface IDataSubjectRequestHandler
{
    /// <summary>Handles the request.</summary>
    Task HandleAsync(string dataSubjectId, string requestType, CancellationToken cancellationToken = default);
}

