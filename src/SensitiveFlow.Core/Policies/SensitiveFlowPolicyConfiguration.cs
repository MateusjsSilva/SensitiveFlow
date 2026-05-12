using SensitiveFlow.Core.Profiles;

namespace SensitiveFlow.Core.Policies;

/// <summary>
/// Helper methods for constructing SensitiveFlow policy options without a DI dependency.
/// </summary>
public static class SensitiveFlowPolicyConfiguration
{
    /// <summary>Creates and configures a new options instance.</summary>
    public static SensitiveFlowOptions Create(Action<SensitiveFlowOptions>? configure = null)
    {
        var options = new SensitiveFlowOptions();
        configure?.Invoke(options);
        return options;
    }
}
