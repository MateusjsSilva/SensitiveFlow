namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Exception thrown when SensitiveFlow is configured inconsistently.
/// </summary>
public sealed class SensitiveFlowConfigurationException : SensitiveFlowException
{
    /// <summary>Initializes a new instance.</summary>
    public SensitiveFlowConfigurationException(string message, string code, Exception? innerException = null)
        : base(message, code, innerException)
    {
    }
}

