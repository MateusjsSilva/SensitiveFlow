using FluentAssertions;
using SensitiveFlow.Core.Diagnostics;

namespace SensitiveFlow.Core.Tests;

public sealed class RedactionPerformanceProfilerTests
{
    [Fact]
    public void Profile_RecordsOperationTiming()
    {
        var profiler = new RedactionPerformanceProfiler();

        using (profiler.Profile("test-op", "field1", 100))
        {
            System.Threading.Thread.Sleep(10);
        }

        var metrics = profiler.GetMetrics("test-op");

        metrics.Should().HaveCount(1);
        metrics[0].OperationName.Should().Be("test-op");
        metrics[0].FieldName.Should().Be("field1");
        metrics[0].DataSizeBytes.Should().Be(100);
        metrics[0].ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Profile_RecordsMultipleOperations()
    {
        var profiler = new RedactionPerformanceProfiler();

        for (int i = 0; i < 5; i++)
        {
            using (profiler.Profile("mask-email", "email", 50))
            {
                System.Threading.Thread.Sleep(5);
            }
        }

        var metrics = profiler.GetMetrics("mask-email");

        metrics.Should().HaveCount(5);
        metrics.Should().AllSatisfy(m =>
        {
            m.OperationName.Should().Be("mask-email");
            m.FieldName.Should().Be("email");
            m.DataSizeBytes.Should().Be(50);
        });
    }

    [Fact]
    public void GenerateReport_CalculatesCorrectStatistics()
    {
        var profiler = new RedactionPerformanceProfiler();

        using (profiler.Profile("test-op", "field1", 100))
        {
            System.Threading.Thread.Sleep(10);
        }

        using (profiler.Profile("test-op", "field1", 100))
        {
            System.Threading.Thread.Sleep(20);
        }

        var report = profiler.GenerateReport();

        report.TotalOperations.Should().Be(2);
        report.Summaries.Should().HaveCount(1);

        var summary = report.Summaries[0];
        summary.OperationName.Should().Be("test-op");
        summary.ExecutionCount.Should().Be(2);
        summary.TotalDataSize.Should().Be(200);
        summary.AverageMs.Should().BeGreaterThan(10);
        summary.MinMs.Should().BeGreaterThanOrEqualTo(10);
        summary.MaxMs.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void GenerateReport_CalculatesThroughput()
    {
        var profiler = new RedactionPerformanceProfiler();

        using (profiler.Profile("test-op", "field1", 1000))
        {
            System.Threading.Thread.Sleep(100);
        }

        var report = profiler.GenerateReport();

        var summary = report.Summaries[0];
        summary.ThroughputMbPerSec.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProfileReport_FormatsToReadableString()
    {
        var profiler = new RedactionPerformanceProfiler();

        using (profiler.Profile("mask-email", "email", 100))
        {
            System.Threading.Thread.Sleep(5);
        }

        var report = profiler.GenerateReport();
        var str = report.ToString();

        str.Should().Contain("RedactionPerformanceProfiler Report");
        str.Should().Contain("Total Operations: 1");
        str.Should().Contain("mask-email");
    }

    [Fact]
    public void OperationSummary_FormatsToReadableString()
    {
        var summary = new RedactionPerformanceProfiler.OperationSummary(
            "mask-email",
            ExecutionCount: 100,
            TotalElapsedMs: 500,
            AverageMs: 5.0,
            MinMs: 2,
            MaxMs: 10,
            TotalDataSize: 10000,
            ThroughputMbPerSec: 20.0);

        var str = summary.ToString();

        str.Should().Contain("mask-email");
        str.Should().Contain("100 ops");
        str.Should().Contain("5.00ms");
        str.Should().Contain("2/10ms");
    }

    [Fact]
    public void GetMetrics_ReturnsEmptyForUnknownOperation()
    {
        var profiler = new RedactionPerformanceProfiler();

        var metrics = profiler.GetMetrics("unknown-op");

        metrics.Should().BeEmpty();
    }

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        var profiler = new RedactionPerformanceProfiler();

        using (profiler.Profile("test-op", "field1", 100))
        {
            System.Threading.Thread.Sleep(5);
        }

        profiler.GetMetrics("test-op").Should().HaveCount(1);

        profiler.Reset();

        profiler.GetMetrics("test-op").Should().BeEmpty();
    }

    [Fact]
    public void ProfileToken_IsDisposable()
    {
        var profiler = new RedactionPerformanceProfiler();
        RedactionPerformanceProfiler.ProfileToken token;

        using (token = profiler.Profile("test-op", "field1", 100))
        {
            System.Threading.Thread.Sleep(5);
        }

        var metrics = profiler.GetMetrics("test-op");

        metrics.Should().HaveCount(1);
        metrics[0].ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void BenchmarkEmailMasking_RunsSuccessfully()
    {
        var profiler = new RedactionPerformanceProfiler();

        RedactionBenchmarks.BenchmarkEmailMasking(profiler, iterations: 100);

        var metrics = profiler.GetMetrics("mask-email");

        metrics.Should().NotBeEmpty();
        metrics.Should().HaveCount(300); // 3 email types * 100 iterations
    }

    [Fact]
    public void BenchmarkPhoneMasking_RunsSuccessfully()
    {
        var profiler = new RedactionPerformanceProfiler();

        RedactionBenchmarks.BenchmarkPhoneMasking(profiler, iterations: 100);

        var metrics = profiler.GetMetrics("mask-phone");

        metrics.Should().NotBeEmpty();
        metrics.Should().HaveCount(300); // 3 phone types * 100 iterations
    }

    [Fact]
    public void BenchmarkSHA256_RunsSuccessfully()
    {
        var profiler = new RedactionPerformanceProfiler();

        RedactionBenchmarks.BenchmarkSHA256Hashing(profiler, iterations: 10);

        var metrics = profiler.GetMetrics("sha256-hash");

        metrics.Should().NotBeEmpty();
        metrics.Should().HaveCount(30); // 3 sizes * 10 iterations
    }

    [Fact]
    public void GenerateReport_SortsBySlowestOperations()
    {
        var profiler = new RedactionPerformanceProfiler();

        using (profiler.Profile("fast-op", "field1", 100))
        {
            System.Threading.Thread.Sleep(5);
        }

        using (profiler.Profile("slow-op", "field1", 100))
        {
            System.Threading.Thread.Sleep(50);
        }

        var report = profiler.GenerateReport();

        report.Summaries[0].OperationName.Should().Be("slow-op");
        report.Summaries[1].OperationName.Should().Be("fast-op");
    }
}
