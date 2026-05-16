using System.Text.Json;
using System.Text.Json.Serialization;

namespace SensitiveFlow.Retention.Analytics;

/// <summary>
/// Generates formatted reports from retention analytics data.
/// </summary>
public static class RetentionReportGenerator
{
    /// <summary>
    /// Generates a plain text summary report.
    /// </summary>
    /// <param name="summary">The trend summary to report on.</param>
    /// <returns>A formatted text report.</returns>
    public static string GenerateTextReport(RetentionTrendSummary summary)
    {
        if (summary == null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        var lines = new List<string>
        {
            "=== Retention Analytics Report ===",
            "",
            $"Total Runs: {summary.TotalRuns}",
            $"Total Fields Anonymized: {summary.TotalAnonymized}",
            $"Total Entities Marked for Deletion: {summary.TotalDeletePending}",
            $"Average Anonymized per Run: {summary.AverageAnonymizedPerRun:F2}",
            ""
        };

        if (summary.LastRunAt.HasValue)
        {
            lines.Add($"Last Run: {summary.LastRunAt:O}");
        }

        if (summary.PeakAnonymizedRun != null)
        {
            lines.Add($"Peak Anonymized Count: {summary.PeakAnonymizedRun.AnonymizedCount} (on {summary.PeakAnonymizedRun.RunAt:O})");
        }

        lines.Add("");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Generates a CSV report of run history.
    /// </summary>
    /// <param name="records">The run records to report on.</param>
    /// <returns>A CSV-formatted report with header and data rows.</returns>
    public static string GenerateCsvReport(IReadOnlyList<RetentionRunRecord> records)
    {
        if (records == null)
        {
            throw new ArgumentNullException(nameof(records));
        }

        var lines = new List<string>
        {
            "RunAt,AnonymizedCount,DeletePendingCount,DurationMs"
        };

        foreach (var record in records)
        {
            lines.Add($"{record.RunAt:O},{record.AnonymizedCount},{record.DeletePendingCount},{record.DurationMs:F2}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Generates a JSON report of trend summary.
    /// </summary>
    /// <param name="summary">The trend summary to report on.</param>
    /// <returns>A JSON-formatted report string.</returns>
    public static string GenerateJsonReport(RetentionTrendSummary summary)
    {
        if (summary == null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(summary, options);
    }
}
