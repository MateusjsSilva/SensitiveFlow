namespace SensitiveFlow.HealthChecks.AuditTracking;

/// <summary>
/// Tracks the age of audit records and warns if they exceed thresholds.
/// </summary>
public sealed class AuditAgeTracker
{
    /// <summary>
    /// Default warning threshold (30 days).
    /// </summary>
    public const int DefaultWarningDaysThreshold = 30;

    /// <summary>
    /// Default critical threshold (90 days).
    /// </summary>
    public const int DefaultCriticalDaysThreshold = 90;

    private int _warningDaysThreshold = DefaultWarningDaysThreshold;
    private int _criticalDaysThreshold = DefaultCriticalDaysThreshold;

    /// <summary>
    /// Gets or sets the warning threshold in days.
    /// </summary>
    public int WarningDaysThreshold
    {
        get => _warningDaysThreshold;
        set => _warningDaysThreshold = value > 0 ? value : throw new ArgumentException("Must be positive", nameof(value));
    }

    /// <summary>
    /// Gets or sets the critical threshold in days.
    /// </summary>
    public int CriticalDaysThreshold
    {
        get => _criticalDaysThreshold;
        set => _criticalDaysThreshold = value > 0 ? value : throw new ArgumentException("Must be positive", nameof(value));
    }

    /// <summary>
    /// Analyzes audit record age and returns health status.
    /// </summary>
    public AuditAgeAnalysis Analyze(DateTime oldestAuditRecord)
    {
        var ageInDays = (DateTime.UtcNow - oldestAuditRecord).TotalDays;
        var status = AuditAgeStatus.Healthy;
        var message = $"Oldest audit record is {ageInDays:F1} days old";

        if (ageInDays > CriticalDaysThreshold)
        {
            status = AuditAgeStatus.Critical;
            message = $"Audit records are {ageInDays:F1} days old (critical threshold: {CriticalDaysThreshold} days)";
        }
        else if (ageInDays > WarningDaysThreshold)
        {
            status = AuditAgeStatus.Warning;
            message = $"Audit records are {ageInDays:F1} days old (warning threshold: {WarningDaysThreshold} days)";
        }

        return new AuditAgeAnalysis
        {
            OldestRecordDate = oldestAuditRecord,
            AgeInDays = ageInDays,
            Status = status,
            Message = message,
            AnalyzedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Analyzes multiple audit record dates and returns aggregate status.
    /// </summary>
    public AuditAgeAnalysis AnalyzeByCategory(Dictionary<string, DateTime> categoryAges)
    {
        ArgumentNullException.ThrowIfNull(categoryAges);

        if (categoryAges.Count == 0)
        {
            return new AuditAgeAnalysis
            {
                Status = AuditAgeStatus.Healthy,
                Message = "No audit records to analyze",
                AnalyzedAt = DateTime.UtcNow
            };
        }

        var oldestRecord = categoryAges.Values.Min();
        var analysis = Analyze(oldestRecord);

        var categorySummary = string.Join(", ", categoryAges.Select(kv =>
        {
            var age = (DateTime.UtcNow - kv.Value).TotalDays;
            return $"{kv.Key}: {age:F1}d";
        }));

        analysis.Message = $"{analysis.Message}. Categories: {categorySummary}";
        analysis.CategoryAges = categoryAges;

        return analysis;
    }

    /// <summary>
    /// Gets recommendations based on audit age.
    /// </summary>
    public string[] GetRecommendations(AuditAgeAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var recommendations = new List<string>();

        if (analysis.Status == AuditAgeStatus.Critical)
        {
            recommendations.Add("URGENT: Implement audit archival or retention policy");
            recommendations.Add("Consider exporting old records to cold storage (S3, Azure Blob)");
            recommendations.Add("Review database storage capacity and costs");
        }
        else if (analysis.Status == AuditAgeStatus.Warning)
        {
            recommendations.Add("Consider implementing retention policy to age out old records");
            recommendations.Add("Plan audit archival for cost optimization");
        }

        recommendations.Add("Monitor audit record growth rate monthly");
        recommendations.Add("Ensure audit cleanup job is running as scheduled");

        return recommendations.ToArray();
    }
}

/// <summary>
/// Analysis result for audit record age.
/// </summary>
public sealed class AuditAgeAnalysis
{
    /// <summary>Gets the oldest audit record date.</summary>
    public DateTime? OldestRecordDate { get; set; }

    /// <summary>Gets the age of the oldest record in days.</summary>
    public double AgeInDays { get; set; }

    /// <summary>Gets the health status.</summary>
    public AuditAgeStatus Status { get; set; }

    /// <summary>Gets a summary message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets per-category ages if analyzed by category.</summary>
    public Dictionary<string, DateTime>? CategoryAges { get; set; }

    /// <summary>Gets the analysis timestamp.</summary>
    public DateTime AnalyzedAt { get; set; }
}

/// <summary>
/// Health status for audit age.
/// </summary>
public enum AuditAgeStatus
{
    /// <summary>Audit records are recent and healthy.</summary>
    Healthy = 0,

    /// <summary>Audit records are aging and should be reviewed.</summary>
    Warning = 1,

    /// <summary>Audit records are critically old and require immediate action.</summary>
    Critical = 2
}
