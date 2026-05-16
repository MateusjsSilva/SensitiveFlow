using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Diagnostics.ComplianceReporting;

/// <summary>
/// Generates automated compliance reports from audit trail data.
/// </summary>
public sealed class ComplianceReportService
{
    private readonly IAuditStore _auditStore;

    /// <summary>Initializes a new instance with the provided audit store.</summary>
    public ComplianceReportService(IAuditStore auditStore)
    {
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
    }

    /// <summary>
    /// Generate audit frequency report for a time period.
    /// </summary>
    public async Task<AuditFrequencyReport> GenerateAuditFrequencyReportAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        var records = await _auditStore.QueryAsync(
            new AuditQuery()
                .InTimeRange(startDate, endDate)
                .WithPagination(0, int.MaxValue));

        var byOperation = records
            .GroupBy(r => r.Operation)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        var byEntity = records
            .GroupBy(r => r.Entity)
            .ToDictionary(g => g.Key, g => g.Count());

        var byActor = records
            .Where(r => r.ActorId != null)
            .GroupBy(r => r.ActorId!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        return new AuditFrequencyReport
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalAuditRecords = records.Count,
            OperationCounts = byOperation,
            EntityCounts = byEntity.ToDictionary(k => k.Key, v => v.Value),
            TopActors = byActor,
            DailyAverage = records.Count / (endDate.Date - startDate.Date).Days
        };
    }

    /// <summary>
    /// Generate data subject coverage report (which subjects are audited).
    /// </summary>
    public async Task<DataSubjectCoverageReport> GenerateDataSubjectCoverageReportAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        var records = await _auditStore.QueryAsync(
            new AuditQuery()
                .InTimeRange(startDate, endDate)
                .WithPagination(0, int.MaxValue));

        var uniqueSubjects = records
            .Select(r => r.DataSubjectId)
            .Distinct()
            .Count();

        var subjectsWithAccess = records
            .Where(r => r.Operation == Core.Enums.AuditOperation.Access)
            .Select(r => r.DataSubjectId)
            .Distinct()
            .Count();

        var subjectsWithModification = records
            .Where(r => r.Operation is Core.Enums.AuditOperation.Create
                      or Core.Enums.AuditOperation.Update
                      or Core.Enums.AuditOperation.Delete)
            .Select(r => r.DataSubjectId)
            .Distinct()
            .Count();

        return new DataSubjectCoverageReport
        {
            StartDate = startDate,
            EndDate = endDate,
            UniqueDataSubjects = uniqueSubjects,
            SubjectsWithAccessAudit = subjectsWithAccess,
            SubjectsWithModificationAudit = subjectsWithModification,
            CoveragePercentage = uniqueSubjects > 0
                ? (subjectsWithModification * 100.0m) / uniqueSubjects
                : 0
        };
    }

    /// <summary>
    /// Generate retention compliance report (check for proper deletion/anonymization).
    /// </summary>
    public async Task<RetentionComplianceReport> GenerateRetentionComplianceReportAsync(
        int retentionDays)
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        var oldRecords = await _auditStore.QueryAsync(
            new AuditQuery()
                .InTimeRange(null, cutoffDate)
                .WithPagination(0, int.MaxValue));

        var oldSubjects = oldRecords
            .Select(r => r.DataSubjectId)
            .Distinct()
            .ToList();

        var deletedSubjects = oldRecords
            .Where(r => r.Operation == Core.Enums.AuditOperation.Delete)
            .Select(r => r.DataSubjectId)
            .Distinct()
            .Count();

        return new RetentionComplianceReport
        {
            RetentionDays = retentionDays,
            CutoffDate = cutoffDate,
            OldAuditRecords = oldRecords.Count,
            SubjectsEligibleForDeletion = oldSubjects.Count,
            SubjectsWithDeletionAudit = deletedSubjects,
            CompliancePercentage = oldSubjects.Count > 0
                ? (deletedSubjects * 100m) / oldSubjects.Count
                : 100m  // No old records = compliant
        };
    }
}

/// <summary>
/// Report: Audit frequency and distribution.
/// </summary>
public class AuditFrequencyReport
{
    /// <summary>Report start date.</summary>
    public DateTimeOffset StartDate { get; set; }
    /// <summary>Report end date.</summary>
    public DateTimeOffset EndDate { get; set; }
    /// <summary>Total audit records in the period.</summary>
    public int TotalAuditRecords { get; set; }
    /// <summary>Audit counts grouped by operation type.</summary>
    public Dictionary<string, int> OperationCounts { get; set; } = new();
    /// <summary>Audit counts grouped by entity.</summary>
    public Dictionary<string, int> EntityCounts { get; set; } = new();
    /// <summary>Top 10 actors by audit record count.</summary>
    public Dictionary<string, int> TopActors { get; set; } = new();
    /// <summary>Average audit records per day.</summary>
    public int DailyAverage { get; set; }
}

/// <summary>
/// Report: Data subject coverage in audit trail.
/// </summary>
public class DataSubjectCoverageReport
{
    /// <summary>Report start date.</summary>
    public DateTimeOffset StartDate { get; set; }
    /// <summary>Report end date.</summary>
    public DateTimeOffset EndDate { get; set; }
    /// <summary>Total unique data subjects audited.</summary>
    public int UniqueDataSubjects { get; set; }
    /// <summary>Data subjects with access audit records.</summary>
    public int SubjectsWithAccessAudit { get; set; }
    /// <summary>Data subjects with modification audit records.</summary>
    public int SubjectsWithModificationAudit { get; set; }
    /// <summary>Coverage percentage (modifications/total).</summary>
    public decimal CoveragePercentage { get; set; }
}

/// <summary>
/// Report: Retention policy compliance.
/// </summary>
public class RetentionComplianceReport
{
    /// <summary>Data retention policy days.</summary>
    public int RetentionDays { get; set; }
    /// <summary>Cutoff date for old records.</summary>
    public DateTimeOffset CutoffDate { get; set; }
    /// <summary>Audit records older than retention policy.</summary>
    public int OldAuditRecords { get; set; }
    /// <summary>Data subjects eligible for deletion based on retention policy.</summary>
    public int SubjectsEligibleForDeletion { get; set; }
    /// <summary>Data subjects with deletion audit records.</summary>
    public int SubjectsWithDeletionAudit { get; set; }
    /// <summary>Compliance percentage (deleted/eligible).</summary>
    public decimal CompliancePercentage { get; set; }
}
