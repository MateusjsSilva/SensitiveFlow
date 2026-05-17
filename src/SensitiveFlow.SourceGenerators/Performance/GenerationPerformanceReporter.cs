using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SensitiveFlow.SourceGenerators.Performance;

/// <summary>
/// Reports performance metrics for code generation.
/// </summary>
public sealed class GenerationPerformanceReporter
{
    private readonly List<GenerationMetric> _metrics = new();

    /// <summary>
    /// Gets all recorded metrics.
    /// </summary>
    public IReadOnlyList<GenerationMetric> Metrics => _metrics.AsReadOnly();

    /// <summary>
    /// Records a generation operation.
    /// </summary>
    public void RecordOperation(string typeName, string operation, long elapsedMilliseconds, int linesGenerated)
    {
        if (typeName == null) throw new ArgumentNullException(nameof(typeName));
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        _metrics.Add(new GenerationMetric
        {
            TypeName = typeName,
            Operation = operation,
            ElapsedMilliseconds = elapsedMilliseconds,
            LinesGenerated = linesGenerated,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets the total lines of code generated.
    /// </summary>
    public long GetTotalLinesGenerated()
    {
        return _metrics.Sum(m => m.LinesGenerated);
    }

    /// <summary>
    /// Gets the total generation time.
    /// </summary>
    public long GetTotalGenerationTime()
    {
        return _metrics.Sum(m => m.ElapsedMilliseconds);
    }

    /// <summary>
    /// Gets generation throughput (lines per millisecond).
    /// </summary>
    public double GetThroughput()
    {
        var totalTime = GetTotalGenerationTime();
        return totalTime > 0 ? (double)GetTotalLinesGenerated() / totalTime : 0;
    }

    /// <summary>
    /// Generates a performance report.
    /// </summary>
    public string GenerateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Source Generation Performance Report");
        sb.AppendLine();
        sb.AppendLine($"**Total Types Processed**: {_metrics.Select(m => m.TypeName).Distinct().Count()}");
        sb.AppendLine($"**Total Lines Generated**: {GetTotalLinesGenerated():N0}");
        sb.AppendLine($"**Total Time (ms)**: {GetTotalGenerationTime()}");
        sb.AppendLine($"**Generation Throughput**: {GetThroughput():F2} lines/ms");
        return sb.ToString();
    }
}

/// <summary>
/// A single generation metric.
/// </summary>
public sealed class GenerationMetric
{
    /// <summary>Gets the type name.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Gets the operation name.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Gets the elapsed time in milliseconds.</summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>Gets the number of lines of code generated.</summary>
    public int LinesGenerated { get; set; }

    /// <summary>Gets the timestamp of the operation.</summary>
    public DateTime Timestamp { get; set; }
}
