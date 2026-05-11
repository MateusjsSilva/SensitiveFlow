using System.Threading;

namespace SensitiveFlow.Core.Correlation;

/// <summary>
/// Async-local correlation holder for non-ASP.NET code paths.
/// </summary>
public static class SensitiveFlowCorrelation
{
    private static readonly AsyncLocal<AuditCorrelationSnapshot?> CurrentSnapshot = new();

    /// <summary>Gets the current correlation snapshot.</summary>
    public static AuditCorrelationSnapshot? Current
    {
        get => CurrentSnapshot.Value;
        set => CurrentSnapshot.Value = value;
    }
}

