namespace SensitiveFlow.Diagnostics.Validation;

/// <summary>
/// One SensitiveFlow configuration diagnostic.
/// </summary>
public sealed record SensitiveFlowConfigurationDiagnostic
{
    /// <summary>Gets the diagnostic code.</summary>
    public required string Code { get; init; }

    /// <summary>Gets the diagnostic message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the diagnostic severity.</summary>
    public SensitiveFlowDiagnosticSeverity Severity { get; init; } = SensitiveFlowDiagnosticSeverity.Warning;
}

