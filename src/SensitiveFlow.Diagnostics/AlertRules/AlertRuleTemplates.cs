namespace SensitiveFlow.Diagnostics.AlertRules;

/// <summary>
/// Pre-built alert rule templates for common security and compliance scenarios.
/// </summary>
public static class AlertRuleTemplates
{
    /// <summary>
    /// Alert rule: High latency in audit operations (p95 > 50ms).
    /// </summary>
    public static AlertRule HighAuditLatency => new()
    {
        Name = "HighAuditLatency",
        Description = "Audit append operations exceed 50ms at p95",
        Query = "histogram_quantile(0.95, rate(sensitiveflow_audit_append_duration[5m])) > 50",
        Severity = AlertSeverity.Warning,
        For = TimeSpan.FromMinutes(5),
        Annotations = new Dictionary<string, string>
        {
            ["summary"] = "High audit latency detected",
            ["description"] = "Check audit database performance, consider adding indexes"
        }
    };

    /// <summary>
    /// Alert rule: Bulk delete operations detected (more than 50 records in 1 hour).
    /// </summary>
    public static AlertRule BulkDeleteDetected => new()
    {
        Name = "BulkDeleteDetected",
        Description = "More than 50 delete operations in a 1-hour window",
        Query = "sum(rate(sensitiveflow_audit_append_count{audit_operation=\"Delete\"}[1h])) * 3600 > 50",
        Severity = AlertSeverity.Critical,
        For = TimeSpan.FromMinutes(2),
        Annotations = new Dictionary<string, string>
        {
            ["summary"] = "Suspicious bulk delete activity detected",
            ["description"] = "Investigate if bulk delete was authorized"
        }
    };

    /// <summary>
    /// Alert rule: Audit throughput dropped (less than 10 records/min).
    /// </summary>
    public static AlertRule AuditThroughputDrop => new()
    {
        Name = "AuditThroughputDrop",
        Description = "Audit record throughput below 10 records per minute",
        Query = "rate(sensitiveflow_audit_append_count[1m]) < 10",
        Severity = AlertSeverity.Warning,
        For = TimeSpan.FromMinutes(5),
        Annotations = new Dictionary<string, string>
        {
            ["summary"] = "Audit throughput dropped significantly",
            ["description"] = "Check if audit store is responsive or if application activity decreased"
        }
    };

    /// <summary>
    /// Alert rule: Compliance violation detected.
    /// </summary>
    public static AlertRule ComplianceViolation => new()
    {
        Name = "ComplianceViolation",
        Description = "Compliance violation recorded",
        Query = "rate(sensitiveflow_compliance_violations[1m]) > 0",
        Severity = AlertSeverity.Critical,
        For = TimeSpan.FromSeconds(0),  // Alert immediately
        Annotations = new Dictionary<string, string>
        {
            ["summary"] = "Compliance violation detected",
            ["description"] = "Review compliance violations immediately"
        }
    };

    /// <summary>
    /// Alert rule: Slow redaction operations (p95 > 5ms).
    /// </summary>
    public static AlertRule SlowRedaction => new()
    {
        Name = "SlowRedaction",
        Description = "Redaction operations slower than 5ms at p95",
        Query = "histogram_quantile(0.95, rate(sensitiveflow_redaction_duration[5m])) > 5",
        Severity = AlertSeverity.Info,
        For = TimeSpan.FromMinutes(10),
        Annotations = new Dictionary<string, string>
        {
            ["summary"] = "Redaction performance degradation",
            ["description"] = "Consider optimizing reflection cache or masking strategies"
        }
    };

    /// <summary>
    /// Alert rule: Sensitive field access spike (> 1000 accesses/min).
    /// </summary>
    public static AlertRule SensitiveFieldAccessSpike => new()
    {
        Name = "SensitiveFieldAccessSpike",
        Description = "Unexpected spike in sensitive field access (> 1000/min)",
        Query = "rate(sensitiveflow_sensitive_fields_accessed[1m]) > 1000",
        Severity = AlertSeverity.Warning,
        For = TimeSpan.FromMinutes(2),
        Annotations = new Dictionary<string, string>
        {
            ["summary"] = "Unusual access pattern to sensitive fields",
            ["description"] = "Investigate if legitimate or potential data exfiltration attempt"
        }
    };

    /// <summary>
    /// Get all alert templates.
    /// </summary>
    public static IEnumerable<AlertRule> GetAllTemplates() => new[]
    {
        HighAuditLatency,
        BulkDeleteDetected,
        AuditThroughputDrop,
        ComplianceViolation,
        SlowRedaction,
        SensitiveFieldAccessSpike
    };
}

/// <summary>
/// Represents an alert rule configuration.
/// </summary>
public class AlertRule
{
    /// <summary>Name of the alert rule.</summary>
    public required string Name { get; set; }

    /// <summary>Description of what the alert detects.</summary>
    public required string Description { get; set; }

    /// <summary>Prometheus query expression for the alert.</summary>
    public required string Query { get; set; }

    /// <summary>Severity level of the alert.</summary>
    public required AlertSeverity Severity { get; set; }

    /// <summary>Duration before alert fires after condition is met.</summary>
    public required TimeSpan For { get; set; }

    /// <summary>Alert annotations (summary, description, etc.).</summary>
    public required Dictionary<string, string> Annotations { get; set; }
}

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    /// <summary>Informational alert.</summary>
    Info,

    /// <summary>Warning alert requiring attention.</summary>
    Warning,

    /// <summary>Critical alert requiring immediate action.</summary>
    Critical
}
