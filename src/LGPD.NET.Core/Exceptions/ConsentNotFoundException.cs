using LGPD.NET.Core.Enums;

namespace LGPD.NET.Core.Exceptions;

public sealed class ConsentNotFoundException : Exception
{
    public string DataSubjectId { get; }
    public ProcessingPurpose Purpose { get; }

    public ConsentNotFoundException(string dataSubjectId, ProcessingPurpose purpose)
        : base($"Consent not found for data subject '{dataSubjectId}' for purpose '{purpose}'.")
    {
        DataSubjectId = dataSubjectId;
        Purpose = purpose;
    }
}
