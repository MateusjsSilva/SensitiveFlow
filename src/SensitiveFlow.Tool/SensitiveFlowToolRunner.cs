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

        var inputPath = Path.GetFullPath(args[1]);
        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            error.WriteLine($"Assembly or directory not found: {inputPath}");
            return 3;
        }

        var outputDirectory = args.Length >= 3
            ? Path.GetFullPath(args[2])
            : Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDirectory);

        var assemblies = ResolveAssemblies(inputPath).ToArray();
        if (assemblies.Length == 0)
        {
            error.WriteLine($"No assemblies found to scan: {inputPath}");
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
}
