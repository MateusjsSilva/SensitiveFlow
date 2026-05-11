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

    [Fact]
    public void Run_ScanDirectoryWithSingleInvalidProject_BuildsThatProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-project-dir-tests", Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(root, "report");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "AnnotatedProject.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>not-a-real-tfm</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", root, outputDirectory], output, error);

        exitCode.Should().Be(5);
        output.ToString().Should().Contain("Building source project before scan");
        error.ToString().Should().Contain("dotnet build failed");
    }

    [Fact]
    public void Run_ScanDirectoryWithInvalidSolution_BuildsThatSolution()
    {
        var root = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-sln-dir-tests", Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(root, "report");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Broken.slnx"), "<Solution><Project Path=\"Missing.csproj\" /></Solution>");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", root, outputDirectory], output, error);

        exitCode.Should().Be(5);
        output.ToString().Should().Contain("Broken.slnx");
        error.ToString().Should().Contain("dotnet build failed");
    }

    [Fact]
    public void Run_ScanMissingInput_ReturnsNotFoundError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var missingPath = Path.Combine(Path.GetTempPath(), "sensitiveflow-missing", Guid.NewGuid().ToString("N"));

        var exitCode = SensitiveFlowToolRunner.Run(["scan", missingPath], output, error);

        exitCode.Should().Be(3);
        error.ToString().Should().Contain("not found");
    }

    [Fact]
    public void Run_ScanDirectoryWithoutAssemblies_ReturnsNoAssembliesError()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-empty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", directory], output, error);

        exitCode.Should().Be(4);
        error.ToString().Should().Contain("No assemblies found");
    }

    [Fact]
    public void Run_ScanSourceInput_WarnsWhenInMemoryOutboxIsNotDebugOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-source-warning", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Program.cs"), "services.AddInMemoryAuditOutbox();");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", root], output, error);

        exitCode.Should().Be(4);
        error.ToString().Should().Contain("SF-CLI-001");
    }

    [Fact]
    public void Run_ScanSourceInput_DoesNotWarnWhenInMemoryOutboxIsDebugOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-source-debug", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Program.cs"), """
            #if DEBUG
            services.AddInMemoryAuditOutbox();
            #endif
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", root], output, error);

        exitCode.Should().Be(4);
        error.ToString().Should().NotContain("SF-CLI-001");
    }

    [Fact]
    public void Run_ScanSourceInput_WarnsWhenInMemoryOutboxIsInElseBranch()
    {
        var root = Path.Combine(Path.GetTempPath(), "sensitiveflow-tool-source-else", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Program.cs"), """
            #if DEBUG
            services.AddEfCoreAuditOutbox();
            #else
            services.AddInMemoryAuditOutbox();
            #endif
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SensitiveFlowToolRunner.Run(["scan", root], output, error);

        exitCode.Should().Be(4);
        error.ToString().Should().Contain("SF-CLI-001");
    }
}
#endif
