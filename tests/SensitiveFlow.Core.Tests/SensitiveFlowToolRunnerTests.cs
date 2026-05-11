#if NET10_0_OR_GREATER
using FluentAssertions;
using SensitiveFlow.Tool;

namespace SensitiveFlow.Core.Tests;

public sealed class SensitiveFlowToolRunnerTests
{
    [Fact]
    public void Run_Scan_WritesJsonAndMarkdownReports()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-tests", Guid.NewGuid().ToString("N"));
        var assemblyPath = typeof(SensitiveFlowToolRunnerTests).Assembly.Location;
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", assemblyPath, outputDirectory], output, error);

        exitCode.Should().Be(0);
        File.Exists(Path.Combine(outputDirectory, "sensitiveflow-report.json")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory, "sensitiveflow-report.md")).Should().BeTrue();
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Run_WithoutScanCommand_ReturnsUsageError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run([], output, error);

        exitCode.Should().Be(2);
        error.ToString().Should().Contain("Usage:");
    }
}
#endif
