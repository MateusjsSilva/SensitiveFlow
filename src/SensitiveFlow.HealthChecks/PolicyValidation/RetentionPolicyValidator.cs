namespace SensitiveFlow.HealthChecks.PolicyValidation;

/// <summary>
/// Validates that retention policies are properly configured.
/// </summary>
public sealed class RetentionPolicyValidator
{
    private readonly Dictionary<string, int> _policies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a policy for validation.
    /// </summary>
    public void AddPolicy(string category, int retentionDays)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (retentionDays < 1)
        {
            throw new ArgumentException("Retention days must be positive", nameof(retentionDays));
        }

        _policies[category] = retentionDays;
    }

    /// <summary>
    /// Validates that at least one retention policy is configured.
    /// </summary>
    public PolicyValidationResult Validate()
    {
        var policyCount = _policies.Count;

        if (policyCount == 0)
        {
            return new PolicyValidationResult
            {
                IsValid = false,
                Message = "No retention policies configured",
                PolicyCount = 0,
                Issues = new[] { "At least one retention policy must be registered" }
            };
        }

        var issues = new List<string>();

        foreach (var kvp in _policies)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                issues.Add("Policy found with empty category");
                continue;
            }

            if (kvp.Value < 1)
            {
                issues.Add($"Policy '{kvp.Key}' has invalid retention days: {kvp.Value}");
            }
        }

        return new PolicyValidationResult
        {
            IsValid = issues.Count == 0,
            Message = issues.Count == 0
                ? $"{policyCount} retention policies configured"
                : $"{policyCount} policies with {issues.Count} issues",
            PolicyCount = policyCount,
            Issues = issues.ToArray()
        };
    }

    /// <summary>
    /// Gets the total number of configured policies.
    /// </summary>
    public int GetPolicyCount() => _policies.Count;

    /// <summary>
    /// Checks if a specific category has a policy configured.
    /// </summary>
    public bool HasPolicyForCategory(string category)
    {
        return _policies.ContainsKey(category);
    }
}

/// <summary>
/// Result of retention policy validation.
/// </summary>
public sealed class PolicyValidationResult
{
    /// <summary>Gets whether the configuration is valid.</summary>
    public bool IsValid { get; set; }

    /// <summary>Gets a summary message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets the number of configured policies.</summary>
    public int PolicyCount { get; set; }

    /// <summary>Gets any validation issues found.</summary>
    public string[] Issues { get; set; } = Array.Empty<string>();
}
