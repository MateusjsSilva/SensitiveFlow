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

    [Fact]
    public void Run_ScanDirectory_WritesReports()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-tests", Guid.NewGuid().ToString("N"));
        var assemblyDirectory = Path.GetDirectoryName(typeof(SensitiveFlowToolRunnerTests).Assembly.Location)!;
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", assemblyDirectory, outputDirectory], output, error);

        exitCode.Should().Be(0);
        File.Exists(Path.Combine(outputDirectory, "sensitiveflow-report.json")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory, "sensitiveflow-report.md")).Should().BeTrue();
    }

    [Fact]
    public void Run_ScanInvalidProject_ReturnsBuildError()
    {
        var root = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-project-tests", Guid.NewGuid().ToString("N"));
        var projectDirectory = Path.Combine(root, "AnnotatedProject");
        var outputDirectory = Path.Combine(root, "report");
        Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, "AnnotatedProject.csproj");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>not-a-real-tfm</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", projectPath, outputDirectory], output, error);

        exitCode.Should().Be(5);
        error.ToString().Should().Contain("dotnet build failed");
    }
}
#endif
