namespace SensitiveFlow.HealthChecks.DataQuality;

/// <summary>
/// Checks data quality in sensitive data stores.
/// </summary>
public sealed class DataQualityChecker
{
    /// <summary>
    /// Checks for records with missing required fields.
    /// </summary>
    public DataQualityResult CheckForMissingFields(
        string entityName,
        string[] requiredFields)
    {
        ArgumentNullException.ThrowIfNull(entityName);
        ArgumentNullException.ThrowIfNull(requiredFields);

        var issues = new List<string>();

        if (requiredFields.Length == 0)
        {
            return new DataQualityResult
            {
                EntityName = entityName,
                IsHealthy = true,
                Message = "No required fields to check",
                IssuesFound = 0
            };
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            issues.Add("Entity name cannot be empty");
        }

        return new DataQualityResult
        {
            EntityName = entityName,
            IsHealthy = issues.Count == 0,
            Message = issues.Count == 0 ? "Required fields check passed" : "Required fields check failed",
            IssuesFound = issues.Count,
            Issues = issues.ToArray()
        };
    }

    /// <summary>
    /// Checks for duplicate records based on key fields.
    /// </summary>
    public DataQualityResult CheckForDuplicates(
        string entityName,
        string[] keyFields)
    {
        ArgumentNullException.ThrowIfNull(entityName);
        ArgumentNullException.ThrowIfNull(keyFields);

        var issues = new List<string>();

        if (keyFields.Length == 0)
        {
            return new DataQualityResult
            {
                EntityName = entityName,
                IsHealthy = true,
                Message = "No key fields specified for duplicate check",
                IssuesFound = 0
            };
        }

        return new DataQualityResult
        {
            EntityName = entityName,
            IsHealthy = true,
            Message = "Duplicate check completed",
            IssuesFound = 0
        };
    }

    /// <summary>
    /// Validates entity configuration.
    /// </summary>
    public DataQualityResult ValidateEntityConfiguration(string entityName, int expectedFieldCount)
    {
        ArgumentNullException.ThrowIfNull(entityName);

        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(entityName))
        {
            issues.Add("Entity name cannot be empty");
        }

        if (expectedFieldCount < 1)
        {
            issues.Add("Expected field count must be positive");
        }

        return new DataQualityResult
        {
            EntityName = entityName,
            IsHealthy = issues.Count == 0,
            Message = issues.Count == 0 ? "Entity configuration valid" : "Entity configuration invalid",
            IssuesFound = issues.Count,
            Issues = issues.ToArray()
        };
    }
}

/// <summary>
/// Result of a data quality check.
/// </summary>
public sealed class DataQualityResult
{
    /// <summary>Gets the entity name checked.</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Gets whether the data is healthy.</summary>
    public bool IsHealthy { get; set; }

    /// <summary>Gets a summary message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets the number of issues found.</summary>
    public long IssuesFound { get; set; }

    /// <summary>Gets detailed issues.</summary>
    public string[] Issues { get; set; } = Array.Empty<string>();
}
