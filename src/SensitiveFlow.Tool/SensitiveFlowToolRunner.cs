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
            error.WriteLine("Usage: sensitiveflow scan <assembly-path> [output-directory]");
            return 2;
        }

        var assemblyPath = Path.GetFullPath(args[1]);
        if (!File.Exists(assemblyPath))
        {
            error.WriteLine($"Assembly not found: {assemblyPath}");
            return 3;
        }

        var outputDirectory = args.Length >= 3
            ? Path.GetFullPath(args[2])
            : Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDirectory);

        var assembly = Assembly.LoadFrom(assemblyPath);
        var report = SensitiveDataDiscovery.Scan(assembly);

        var jsonPath = Path.Combine(outputDirectory, "sensitiveflow-report.json");
        var markdownPath = Path.Combine(outputDirectory, "sensitiveflow-report.md");

        File.WriteAllText(jsonPath, report.ToJson());
        File.WriteAllText(markdownPath, report.ToMarkdown());

        output.WriteLine($"SensitiveFlow report written: {jsonPath}");
        output.WriteLine($"SensitiveFlow report written: {markdownPath}");
        return 0;
    }
}
