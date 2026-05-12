namespace SensitiveFlow.Core.Correlation;

/// <summary>
/// Supplies correlation identifiers for audit and diagnostics records.
/// </summary>
public interface IAuditCorrelationContext
{
    /// <summary>Gets the current correlation identifier.</summary>
    string? CorrelationId { get; }

    /// <summary>Gets the current request identifier.</summary>
    string? RequestId { get; }

    /// <summary>Gets the current trace identifier.</summary>
    string? TraceId { get; }
}

