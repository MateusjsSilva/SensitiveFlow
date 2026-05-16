using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Interfaces;

/// <summary>
/// Represents an alert triggered by suspicious audit patterns.
/// </summary>
public sealed record AuditAlert
{
    /// <summary>Unique identifier for this alert.</summary>
    public required string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Alert severity: Info, Warning, Critical.</summary>
    public required string Severity { get; init; }

    /// <summary>Human-readable description of the suspicious pattern.</summary>
    public required string Message { get; init; }

    /// <summary>When the alert was triggered.</summary>
    public DateTimeOffset TriggeredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Related data subject IDs (affected users).</summary>
    public string[]? DataSubjectIds { get; init; }

    /// <summary>Related actor IDs (suspicious users).</summary>
    public string[]? ActorIds { get; init; }

    /// <summary>Related entities affected by the suspicious activity.</summary>
    public string[]? Entities { get; init; }

    /// <summary>Additional context about the alert.</summary>
    public IDictionary<string, object>? Context { get; init; }
}

/// <summary>
/// Policies for detecting suspicious audit patterns (bulk deletes, multiple IPs per subject, etc.).
/// </summary>
public interface IAuditAlertingPolicy
{
    /// <summary>
    /// Analyzes recent audit records for suspicious patterns.
    /// </summary>
    /// <param name="windowMinutes">Look-back window (default: last 60 minutes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of triggered alerts.</returns>
    /// <remarks>
    /// <para>
    /// Checks for:
    /// 1. Bulk deletes: More than N delete operations per entity in the window.
    /// 2. Multiple IPs per subject: Same data subject accessed from >3 different IP tokens in window.
    /// 3. Unusual actors: Actors outside normal working hours or performing unusual operations.
    /// 4. Access after deletion: Attempts to access deleted records.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<AuditAlert>> DetectAnomaliesAsync(
        int windowMinutes = 60,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a custom anomaly detection rule.
    /// </summary>
    /// <param name="ruleName">Unique name for the rule.</param>
    /// <param name="detector">Async function that analyzes records and returns alerts.</param>
    /// <remarks>
    /// Example:
    /// <code>
    /// await policy.RegisterRuleAsync("BulkDelete", async (records) =>
    /// {
    ///     var deletes = records.Where(r => r.Operation == AuditOperation.Delete);
    ///     if (await deletes.CountAsync() > 1000)
    ///         yield return new AuditAlert { Severity = "Critical", Message = "Bulk delete detected" };
    /// });
    /// </code>
    /// </remarks>
    Task RegisterRuleAsync(
        string ruleName,
        Func<IAsyncEnumerable<AuditRecord>, IAsyncEnumerable<AuditAlert>> detector);

    /// <summary>
    /// Unregisters a custom detection rule.
    /// </summary>
    Task UnregisterRuleAsync(string ruleName);

    /// <summary>
    /// Gets all registered rule names.
    /// </summary>
    Task<IReadOnlyList<string>> GetRegisteredRulesAsync();

    /// <summary>
    /// Clears all alerts (after acknowledging or resolving them).
    /// </summary>
    Task ClearAlertsAsync();

    /// <summary>
    /// Gets all current unresolved alerts.
    /// </summary>
    Task<IReadOnlyList<AuditAlert>> GetAlertsAsync();
}
