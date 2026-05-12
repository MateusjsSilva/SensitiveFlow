namespace SensitiveFlow.Diagnostics.Validation;

/// <summary>
/// Result of SensitiveFlow startup configuration validation.
/// </summary>
public sealed class SensitiveFlowConfigurationReport
{
    /// <summary>Initializes a new report.</summary>
    public SensitiveFlowConfigurationReport(IEnumerable<SensitiveFlowConfigurationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = diagnostics.ToArray();
    }

    /// <summary>Gets the diagnostics.</summary>
    public IReadOnlyList<SensitiveFlowConfigurationDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether any error diagnostics were produced.</summary>
    public bool IsValid => !Diagnostics.Any(static d => d.Severity == SensitiveFlowDiagnosticSeverity.Error);
}

