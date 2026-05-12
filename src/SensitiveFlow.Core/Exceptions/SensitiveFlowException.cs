namespace SensitiveFlow.Core.Exceptions;

/// <summary>
/// Base exception type for SensitiveFlow failures with a safe machine-readable code.
/// </summary>
public class SensitiveFlowException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public SensitiveFlowException(string message, string code, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>Gets the safe machine-readable code.</summary>
    public string Code { get; }
}

