using System.Diagnostics;
using System.Reflection;
using SensitiveFlow.Core.Discovery;

namespace SensitiveFlow.Tool;

/// <summary>
/// Command runner for the SensitiveFlow CLI.
/// </summary>
public static class SensitiveFlowToolRunner
{
    /// <summary>Runs the CLI command.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length < 2 || !string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine("Usage: sensitiveflow scan <assembly-project-or-directory> [output-directory]");
            return 2;
        }

        var inputPath = Path.GetFullPath(args[1]);
        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            error.WriteLine($"Assembly, project, solution, or directory not found: {inputPath}");
            return 3;
        }

        var outputDirectory = args.Length >= 3
            ? Path.GetFullPath(args[2])
            : Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDirectory);

        var buildResult = TryBuildSourceInput(inputPath, output, error);
        if (buildResult.ExitCode != 0)
        {
            return buildResult.ExitCode;
        }

        var scanPath = buildResult.ScanPath ?? inputPath;
        var assemblies = ResolveAssemblies(scanPath).ToArray();
        if (assemblies.Length == 0)
        {
            error.WriteLine($"No assemblies found to scan: {scanPath}");
            return 4;
        }

        var report = SensitiveDataDiscovery.Scan(assemblies);

        var jsonPath = Path.Combine(outputDirectory, "sensitiveflow-report.json");
        var markdownPath = Path.Combine(outputDirectory, "sensitiveflow-report.md");

        File.WriteAllText(jsonPath, report.ToJson());
        File.WriteAllText(markdownPath, report.ToMarkdown());

        output.WriteLine($"SensitiveFlow report written: {jsonPath}");
        output.WriteLine($"SensitiveFlow report written: {markdownPath}");
        return 0;
    }

    private static BuildSourceResult TryBuildSourceInput(string inputPath, TextWriter output, TextWriter error)
    {
        var buildTarget = ResolveBuildTarget(inputPath);
        if (buildTarget is null)
        {
            return new BuildSourceResult(0, null);
        }

        output.WriteLine($"Building source project before scan: {buildTarget}");
        var psi = new ProcessStartInfo("dotnet", $"build \"{buildTarget}\" -c Release --nologo")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            error.WriteLine("Failed to start dotnet build.");
            return new BuildSourceResult(5, null);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            error.WriteLine("dotnet build timed out.");
            return new BuildSourceResult(5, null);
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            error.WriteLine(stdout);
            error.WriteLine(stderr);
            error.WriteLine($"dotnet build failed with exit code {process.ExitCode}.");
            return new BuildSourceResult(5, null);
        }

        var scanPath = Directory.Exists(inputPath)
            ? inputPath
            : Path.GetDirectoryName(buildTarget);

        return new BuildSourceResult(0, scanPath);
    }

    private static string? ResolveBuildTarget(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            var extension = Path.GetExtension(inputPath);
            return extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                ? inputPath
                : null;
        }

        if (!Directory.Exists(inputPath))
        {
            return null;
        }

        var solution = Directory.EnumerateFiles(inputPath, "*.slnx", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(inputPath, "*.sln", SearchOption.TopDirectoryOnly))
            .FirstOrDefault();
        if (solution is not null)
        {
            return solution;
        }

        var projects = Directory.EnumerateFiles(inputPath, "*.csproj", SearchOption.TopDirectoryOnly).ToArray();
        return projects.Length == 1 ? projects[0] : null;
    }

    private static IEnumerable<Assembly> ResolveAssemblies(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            yield return Assembly.LoadFrom(inputPath);
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(inputPath, "*.dll", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(file);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            yield return assembly;
        }
    }

    private sealed record BuildSourceResult(int ExitCode, string? ScanPath);
}
