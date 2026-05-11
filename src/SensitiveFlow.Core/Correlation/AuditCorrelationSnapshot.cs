namespace SensitiveFlow.Core.Correlation;

/// <summary>
/// Immutable correlation snapshot for audit and diagnostics flows.
/// </summary>
public sealed record AuditCorrelationSnapshot : IAuditCorrelationContext
{
    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <inheritdoc />
    public string? RequestId { get; init; }

    /// <inheritdoc />
    public string? TraceId { get; init; }
}
