using FluentAssertions;
using SensitiveFlow.SourceGenerators.Performance;
using Xunit;

namespace SensitiveFlow.SourceGenerators.Tests;

public class GenerationPerformanceReporterTests
{
    [Fact]
    public void RecordOperation_StoresMetric()
    {
        var reporter = new GenerationPerformanceReporter();

        reporter.RecordOperation("Customer", "MetadataGeneration", 50, 120);

        reporter.Metrics.Should().HaveCount(1);
        reporter.Metrics[0].TypeName.Should().Be("Customer");
        reporter.Metrics[0].Operation.Should().Be("MetadataGeneration");
        reporter.Metrics[0].ElapsedMilliseconds.Should().Be(50);
        reporter.Metrics[0].LinesGenerated.Should().Be(120);
    }

    [Fact]
    public void RecordOperation_Multiple_AggregatesMetrics()
    {
        var reporter = new GenerationPerformanceReporter();

        reporter.RecordOperation("Customer", "MetadataGeneration", 50, 120);
        reporter.RecordOperation("Order", "MetadataGeneration", 40, 95);
        reporter.RecordOperation("Customer", "SerializationGeneration", 30, 80);

        reporter.Metrics.Should().HaveCount(3);
    }

    [Fact]
    public void GetTotalLinesGenerated_SummarizeLines()
    {
        var reporter = new GenerationPerformanceReporter();

        reporter.RecordOperation("Type1", "Gen", 10, 100);
        reporter.RecordOperation("Type2", "Gen", 20, 200);
        reporter.RecordOperation("Type3", "Gen", 15, 150);

        reporter.GetTotalLinesGenerated().Should().Be(450);
    }

    [Fact]
    public void GetTotalGenerationTime_SummarizesTime()
    {
        var reporter = new GenerationPerformanceReporter();

        reporter.RecordOperation("Type1", "Gen", 100, 50);
        reporter.RecordOperation("Type2", "Gen", 200, 75);
        reporter.RecordOperation("Type3", "Gen", 150, 60);

        reporter.GetTotalGenerationTime().Should().Be(450);
    }

    [Fact]
    public void GetThroughput_CalculatesLinesPerMs()
    {
        var reporter = new GenerationPerformanceReporter();

        reporter.RecordOperation("Type", "Gen", 1000, 1000);  // 1000 lines in 1000ms = 1 line/ms

        reporter.GetThroughput().Should().Be(1.0);
    }

    [Fact]
    public void GetThroughput_WithZeroTime_ReturnsZero()
    {
        var reporter = new GenerationPerformanceReporter();

        reporter.GetThroughput().Should().Be(0);
    }

    [Fact]
    public void GenerateReport_ContainsMetadata()
    {
        var reporter = new GenerationPerformanceReporter();
        reporter.RecordOperation("Customer", "MetadataGen", 100, 500);
        reporter.RecordOperation("Order", "MetadataGen", 80, 400);

        var report = reporter.GenerateReport();

        report.Should().Contain("Source Generation Performance Report");
        report.Should().Contain("Total Types Processed");
        report.Should().Contain("Total Lines Generated");
        report.Should().Contain("900");  // 500 + 400 lines
    }

    [Fact]
    public void Metrics_TracksTimestamp()
    {
        var reporter = new GenerationPerformanceReporter();
        var beforeRecord = DateTime.UtcNow;

        reporter.RecordOperation("Type", "Gen", 10, 50);

        var afterRecord = DateTime.UtcNow;
        reporter.Metrics[0].Timestamp.Should().BeOnOrAfter(beforeRecord);
        reporter.Metrics[0].Timestamp.Should().BeOnOrBefore(afterRecord);
    }

    [Fact]
    public void RecordOperation_ThrowsOnNullTypeName()
    {
        var reporter = new GenerationPerformanceReporter();

        var act = () => reporter.RecordOperation(null!, "Gen", 10, 50);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordOperation_ThrowsOnNullOperation()
    {
        var reporter = new GenerationPerformanceReporter();

        var act = () => reporter.RecordOperation("Type", null!, 10, 50);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerationMetric_StoresAllProperties()
    {
        var metric = new GenerationMetric
        {
            TypeName = "Customer",
            Operation = "MetadataGen",
            ElapsedMilliseconds = 150,
            LinesGenerated = 300,
            Timestamp = DateTime.UtcNow
        };

        metric.TypeName.Should().Be("Customer");
        metric.Operation.Should().Be("MetadataGen");
        metric.ElapsedMilliseconds.Should().Be(150);
        metric.LinesGenerated.Should().Be(300);
    }

    [Fact]
    public void ReporterWithHighVolume_HandlesMultipleOperations()
    {
        var reporter = new GenerationPerformanceReporter();

        for (int i = 0; i < 1000; i++)
        {
            reporter.RecordOperation($"Type{i}", "Gen", 10 + (i % 50), 100 + i);
        }

        reporter.Metrics.Should().HaveCount(1000);
        reporter.GetTotalLinesGenerated().Should().BeGreaterThan(100000);
    }
}
