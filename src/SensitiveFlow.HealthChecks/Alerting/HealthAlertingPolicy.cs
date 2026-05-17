namespace SensitiveFlow.HealthChecks.Alerting;

/// <summary>
/// Defines alerting rules for health check failures.
/// </summary>
public sealed class HealthAlertingPolicy
{
    private readonly List<AlertingRule> _rules = new();

    /// <summary>
    /// Gets all configured alerting rules.
    /// </summary>
    public IReadOnlyList<AlertingRule> Rules => _rules.AsReadOnly();

    /// <summary>
    /// Adds an alert rule for a specific check.
    /// </summary>
    public void AddRule(string checkName, AlertSeverity severity, string? webhookUrl = null, string? slackChannel = null)
    {
        ArgumentNullException.ThrowIfNull(checkName);

        _rules.Add(new AlertingRule
        {
            CheckName = checkName,
            Severity = severity,
            WebhookUrl = webhookUrl,
            SlackChannel = slackChannel,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Finds a rule for a specific check.
    /// </summary>
    public AlertingRule? FindRule(string checkName)
    {
        return _rules.FirstOrDefault(r => r.CheckName == checkName);
    }

    /// <summary>
    /// Gets rules by severity level.
    /// </summary>
    public IEnumerable<AlertingRule> GetRulesBySeverity(AlertSeverity severity)
    {
        return _rules.Where(r => r.Severity == severity);
    }

    /// <summary>
    /// Removes a rule for a specific check.
    /// </summary>
    public bool RemoveRule(string checkName)
    {
        var rule = FindRule(checkName);
        if (rule is null)
        {
            return false;
        }

        return _rules.Remove(rule);
    }

    /// <summary>
    /// Clears all rules.
    /// </summary>
    public void Clear()
    {
        _rules.Clear();
    }
}

/// <summary>
/// An alerting rule for a health check.
/// </summary>
public sealed class AlertingRule
{
    /// <summary>Gets the check name.</summary>
    public string CheckName { get; set; } = string.Empty;

    /// <summary>Gets the alert severity.</summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>Gets the optional webhook URL for POST notifications.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Gets the optional Slack channel for notifications.</summary>
    public string? SlackChannel { get; set; }

    /// <summary>Gets whether this rule should trigger PagerDuty alerts.</summary>
    public bool EnablePagerDuty { get; set; }

    /// <summary>Gets the creation timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets whether the rule has any notification configured.
    /// </summary>
    public bool HasNotificationConfigured =>
        !string.IsNullOrEmpty(WebhookUrl) ||
        !string.IsNullOrEmpty(SlackChannel) ||
        EnablePagerDuty;
}

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    /// <summary>Informational alert.</summary>
    Info = 0,

    /// <summary>Warning alert.</summary>
    Warning = 1,

    /// <summary>Error alert.</summary>
    Error = 2,

    /// <summary>Critical alert.</summary>
    Critical = 3
}
