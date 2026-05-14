using System.Diagnostics;
using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Core.Diagnostics;

/// <summary>
/// Measures and reports performance characteristics of sensitive data redaction operations.
/// </summary>
/// <remarks>
/// <para>
/// This profiler helps identify redaction hot paths and optimization opportunities before
/// deploying to production. It records timing, operation count, and throughput metrics.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// var profiler = new RedactionPerformanceProfiler();
/// 
/// using (profiler.Profile("mask-email", "email"))
/// {
///     email = MaskEmail(email);
/// }
/// 
/// var report = profiler.GenerateReport();
/// Console.WriteLine(report);
/// </code>
/// </para>
/// </remarks>
public sealed class RedactionPerformanceProfiler
{
    private readonly Dictionary<string, List<OperationMetric>> _metrics = new();
    private readonly Stopwatch _stopwatch = new();

    /// <summary>Represents a single redaction operation measurement.</summary>
    public sealed record OperationMetric(
        string OperationName,
        string FieldName,
        long ElapsedMilliseconds,
        int DataSizeBytes,
        DateTimeOffset Timestamp);

    /// <summary>Summary statistics for all measurements of a specific operation.</summary>
    public sealed record OperationSummary(
        string OperationName,
        int ExecutionCount,
        long TotalElapsedMs,
        double AverageMs,
        long MinMs,
        long MaxMs,
        long TotalDataSize,
        double ThroughputMbPerSec)
    {
        /// <summary>Returns a formatted summary string.</summary>
        public override string ToString()
        {
            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{OperationName}: {ExecutionCount} ops, avg {AverageMs:F2}ms, min/max {MinMs}/{MaxMs}ms, throughput {ThroughputMbPerSec:F2} MB/s");
        }
    }

    /// <summary>Complete profiling report.</summary>
    public sealed record ProfileReport(
        DateTimeOffset GeneratedAt,
        IReadOnlyList<OperationSummary> Summaries,
        long TotalElapsedMs,
        int TotalOperations)
    {
        /// <summary>Returns a formatted multiline report.</summary>
        public override string ToString()
        {
            var lines = new List<string>
            {
                $"RedactionPerformanceProfiler Report - {GeneratedAt:O}",
                $"Total Operations: {TotalOperations}",
                $"Total Elapsed: {TotalElapsedMs}ms",
                "---",
            };

            foreach (var summary in Summaries)
            {
                lines.Add(summary.ToString());
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>Profiles a single redaction operation.</summary>
    /// <param name="operationName">The name of the operation (e.g., "mask-email", "redact-phone").</param>
    /// <param name="fieldName">The name of the field being redacted.</param>
    /// <param name="dataSizeBytes">Optional size of the data being redacted (for throughput calculation).</param>
    /// <returns>A disposable token that stops timing when disposed.</returns>
    public ProfileToken Profile(string operationName, string fieldName, int dataSizeBytes = 0)
    {
        if (!_metrics.ContainsKey(operationName))
        {
            _metrics[operationName] = new List<OperationMetric>();
        }

        return new ProfileToken(this, operationName, fieldName, dataSizeBytes);
    }

    /// <summary>Records a completed measurement.</summary>
    private void RecordMetric(string operationName, OperationMetric metric)
    {
        if (_metrics.TryGetValue(operationName, out var list))
        {
            list.Add(metric);
        }
    }

    /// <summary>Generates a summary report of all measurements.</summary>
    public ProfileReport GenerateReport()
    {
        var summaries = new List<OperationSummary>();
        var totalMs = 0L;
        var totalOps = 0;

        foreach (var kvp in _metrics)
        {
            var operationName = kvp.Key;
            var metrics = kvp.Value;

            if (metrics.Count == 0)
            {
                continue;
            }

            var elapsedTimes = metrics.Select(m => m.ElapsedMilliseconds).ToList();
            var totalElapsed = elapsedTimes.Sum();
            var avgElapsed = elapsedTimes.Average();
            var minElapsed = elapsedTimes.Min();
            var maxElapsed = elapsedTimes.Max();
            var totalDataSize = metrics.Sum(m => m.DataSizeBytes);

            var throughputMbPerSec = totalElapsed > 0
                ? (totalDataSize / 1024.0 / 1024.0) / (totalElapsed / 1000.0)
                : 0.0;

            summaries.Add(new OperationSummary(
                operationName,
                metrics.Count,
                totalElapsed,
                avgElapsed,
                minElapsed,
                maxElapsed,
                totalDataSize,
                throughputMbPerSec));

            totalMs += totalElapsed;
            totalOps += metrics.Count;
        }

        return new ProfileReport(
            DateTimeOffset.UtcNow,
            summaries.OrderByDescending(s => s.TotalElapsedMs).ToList(),
            totalMs,
            totalOps);
    }

    /// <summary>Returns all recorded metrics for a specific operation.</summary>
    public IReadOnlyList<OperationMetric> GetMetrics(string operationName)
    {
        return _metrics.TryGetValue(operationName, out var metrics)
            ? metrics.AsReadOnly()
            : new List<OperationMetric>().AsReadOnly();
    }

    /// <summary>Clears all recorded metrics.</summary>
    public void Reset()
    {
        _metrics.Clear();
    }

    /// <summary>Disposable token for automatic timing of a profiled operation.</summary>
    public sealed class ProfileToken : IDisposable
    {
        private readonly RedactionPerformanceProfiler _profiler;
        private readonly string _operationName;
        private readonly string _fieldName;
        private readonly int _dataSizeBytes;
        private readonly Stopwatch _sw;
        private bool _disposed;

        internal ProfileToken(
            RedactionPerformanceProfiler profiler,
            string operationName,
            string fieldName,
            int dataSizeBytes)
        {
            _profiler = profiler;
            _operationName = operationName;
            _fieldName = fieldName;
            _dataSizeBytes = dataSizeBytes;
            _sw = Stopwatch.StartNew();
        }

        /// <summary>Stops timing and records the measurement.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sw.Stop();

            var metric = new OperationMetric(
                _operationName,
                _fieldName,
                _sw.ElapsedMilliseconds,
                _dataSizeBytes,
                DateTimeOffset.UtcNow);

            _profiler.RecordMetric(_operationName, metric);
        }
    }
}

/// <summary>
/// Benchmark for common redaction operations (masking, redacting, pseudonymizing).
/// </summary>
/// <remarks>
/// Run these benchmarks to understand the performance characteristics of different
/// redaction strategies and identify optimization opportunities.
/// </remarks>
public static class RedactionBenchmarks
{
    /// <summary>
    /// Runs a benchmark suite on email masking with varying email lengths.
    /// </summary>
    /// <param name="profiler">The profiler to record results to.</param>
    /// <param name="iterations">Number of iterations per email length.</param>
    public static void BenchmarkEmailMasking(
        RedactionPerformanceProfiler profiler,
        int iterations = 10000)
    {
        var emails = new[]
        {
            "a@x.com",
            "alice@example.com",
            "alice.smith+tag@verylongemaildomain.example.com"
        };

        foreach (var email in emails)
        {
            for (int i = 0; i < iterations; i++)
            {
                using (profiler.Profile("mask-email", "email", email.Length))
                {
                    // Simulate masking
                    var result = MaskEmail(email);
                }
            }
        }
    }

    /// <summary>
    /// Runs a benchmark suite on phone number masking.
    /// </summary>
    public static void BenchmarkPhoneMasking(
        RedactionPerformanceProfiler profiler,
        int iterations = 10000)
    {
        var phones = new[]
        {
            "+1234567890",
            "(555) 123-4567",
            "+55 11 98765-4321"
        };

        foreach (var phone in phones)
        {
            for (int i = 0; i < iterations; i++)
            {
                using (profiler.Profile("mask-phone", "phone", phone.Length))
                {
                    var result = MaskPhone(phone);
                }
            }
        }
    }

    /// <summary>
    /// Runs a benchmark suite on SHA256 hashing (for integrity verification).
    /// </summary>
    public static void BenchmarkSHA256Hashing(
        RedactionPerformanceProfiler profiler,
        int iterations = 1000)
    {
        var sizes = new[] { 100, 1000, 10000 };

        foreach (var size in sizes)
        {
            var data = string.Create(size, 0, static (span, _) =>
            {
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = (char)('a' + (i % 26));
                }
            });

            for (int i = 0; i < iterations; i++)
            {
                using (profiler.Profile("sha256-hash", "audit-record", data.Length))
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
                }
            }
        }
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 1
            ? email[0] + new string('*', at - 1) + email[at..]
            : new string('*', email.Length);
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length <= 2)
        {
            return new string('*', phone.Length);
        }

        return phone[0] + new string('*', phone.Length - 2) + phone[^1];
    }
}
