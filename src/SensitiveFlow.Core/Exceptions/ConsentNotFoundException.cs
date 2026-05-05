using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Exception thrown when a consent record cannot be found.
/// </summary>
public sealed class ConsentNotFoundException : Exception
{
    /// <summary>Data subject identifier used in the lookup.</summary>
    public string DataSubjectId { get; }

    /// <summary>Processing purpose used in the lookup.</summary>
    public ProcessingPurpose Purpose { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentNotFoundException" /> class.
    /// </summary>
    /// <param name="dataSubjectId">Data subject identifier.</param>
    /// <param name="purpose">Processing purpose.</param>
    public ConsentNotFoundException(string dataSubjectId, ProcessingPurpose purpose)
        : base($"Consent not found for data subject '{dataSubjectId}' for purpose '{purpose}'.")
    {
        DataSubjectId = dataSubjectId;
        Purpose = purpose;
    }
}
