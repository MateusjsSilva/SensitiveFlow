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
        => Run(args, output, error, SensitiveFlowToolHost.Instance);

    internal static int Run(string[] args, TextWriter output, TextWriter error, ISensitiveFlowToolHost host)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(host);

        if (args.Length < 2 || !string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine("Usage: sensitiveflow scan <assembly-project-or-directory> [output-directory]");
            return 2;
        }

        var inputPath = Path.GetFullPath(args[1]);
        if (!host.FileExists(inputPath) && !host.DirectoryExists(inputPath))
        {
            error.WriteLine($"Assembly, project, solution, or directory not found: {inputPath}");
            return 3;
        }

        var outputDirectory = args.Length >= 3
            ? Path.GetFullPath(args[2])
            : host.CurrentDirectory;
        host.CreateDirectory(outputDirectory);

        EmitSourceWarnings(inputPath, error, host);

        var buildResult = TryBuildSourceInput(inputPath, output, error, host);
        if (buildResult.ExitCode != 0)
        {
            return buildResult.ExitCode;
        }

        var scanPath = buildResult.ScanPath ?? inputPath;
        var assemblies = ResolveAssemblies(scanPath, host).ToArray();
        if (assemblies.Length == 0)
        {
            error.WriteLine($"No assemblies found to scan: {scanPath}");
            return 4;
        }

        var report = SensitiveDataDiscovery.Scan(assemblies);

        var jsonPath = Path.Combine(outputDirectory, "sensitiveflow-report.json");
        var markdownPath = Path.Combine(outputDirectory, "sensitiveflow-report.md");

        host.WriteAllText(jsonPath, report.ToJson());
        host.WriteAllText(markdownPath, report.ToMarkdown());

        output.WriteLine($"SensitiveFlow report written: {jsonPath}");
        output.WriteLine($"SensitiveFlow report written: {markdownPath}");
        return 0;
    }

    private static BuildSourceResult TryBuildSourceInput(string inputPath, TextWriter output, TextWriter error, ISensitiveFlowToolHost host)
    {
        var buildTarget = ResolveBuildTarget(inputPath, host);
        if (buildTarget is null)
        {
            return new BuildSourceResult(0, null);
        }

        output.WriteLine($"Building source project before scan: {buildTarget}");
        var result = host.Build(buildTarget, TimeSpan.FromMinutes(2));
        if (!result.Started)
        {
            error.WriteLine("Failed to start dotnet build.");
            return new BuildSourceResult(5, null);
        }

        if (result.TimedOut)
        {
            error.WriteLine("dotnet build timed out.");
            return new BuildSourceResult(5, null);
        }

        if (result.ExitCode != 0)
        {
            error.WriteLine(result.StandardOutput);
            error.WriteLine(result.StandardError);
            error.WriteLine($"dotnet build failed with exit code {result.ExitCode}.");
            return new BuildSourceResult(5, null);
        }

        var scanPath = host.DirectoryExists(inputPath)
            ? inputPath
            : Path.GetDirectoryName(buildTarget);

        return new BuildSourceResult(0, scanPath);
    }

    private static string? ResolveBuildTarget(string inputPath, ISensitiveFlowToolHost host)
    {
        if (host.FileExists(inputPath))
        {
            var extension = Path.GetExtension(inputPath);
            return extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                ? inputPath
                : null;
        }

        if (!host.DirectoryExists(inputPath))
        {
            return null;
        }

        var solution = host.EnumerateFiles(inputPath, "*.slnx", SearchOption.TopDirectoryOnly)
            .Concat(host.EnumerateFiles(inputPath, "*.sln", SearchOption.TopDirectoryOnly))
            .FirstOrDefault();
        if (solution is not null)
        {
            return solution;
        }

        var projects = host.EnumerateFiles(inputPath, "*.csproj", SearchOption.TopDirectoryOnly).ToArray();
        return projects.Length == 1 ? projects[0] : null;
    }

    private static void EmitSourceWarnings(string inputPath, TextWriter error, ISensitiveFlowToolHost host)
    {
        foreach (var sourceFile in ResolveSourceFiles(inputPath, host))
        {
            WarnIfInMemoryOutboxIsNotDebugOnly(sourceFile, error, host);
        }
    }

    private static IEnumerable<string> ResolveSourceFiles(string inputPath, ISensitiveFlowToolHost host)
    {
        var root = host.FileExists(inputPath)
            ? Path.GetDirectoryName(inputPath)
            : inputPath;

        if (root is null || !host.DirectoryExists(root))
        {
            yield break;
        }

        foreach (var file in host.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return file;
        }
    }

    private static void WarnIfInMemoryOutboxIsNotDebugOnly(string sourceFile, TextWriter error, ISensitiveFlowToolHost host)
    {
        var conditionalStack = new Stack<bool>();
        var lines = host.ReadLines(sourceFile);
        var lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();

            if (line.StartsWith("#if", StringComparison.Ordinal))
            {
                conditionalStack.Push(line.Contains("DEBUG", StringComparison.Ordinal) && !line.Contains("!DEBUG", StringComparison.Ordinal));
            }
            else if (line.StartsWith("#else", StringComparison.Ordinal) && conditionalStack.Count > 0)
            {
                conditionalStack.Push(!conditionalStack.Pop());
            }
            else if (line.StartsWith("#endif", StringComparison.Ordinal) && conditionalStack.Count > 0)
            {
                conditionalStack.Pop();
            }

            if (!conditionalStack.Contains(true) && rawLine.Contains("AddInMemoryAuditOutbox(", StringComparison.Ordinal))
            {
                error.WriteLine($"SF-CLI-001: AddInMemoryAuditOutbox() found outside #if DEBUG: {sourceFile}:{lineNumber}");
            }
        }
    }

    private static IEnumerable<Assembly> ResolveAssemblies(string inputPath, ISensitiveFlowToolHost host)
    {
        if (host.FileExists(inputPath))
        {
            yield return host.LoadAssembly(inputPath);
            yield break;
        }

        foreach (var file in host.EnumerateFiles(inputPath, "*.dll", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            Assembly assembly;
            try
            {
                assembly = host.LoadAssembly(file);
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

internal interface ISensitiveFlowToolHost
{
    string CurrentDirectory { get; }

    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    IEnumerable<string> ReadLines(string path);

    void WriteAllText(string path, string contents);

    Assembly LoadAssembly(string path);

    SensitiveFlowToolBuildResult Build(string buildTarget, TimeSpan timeout);
}

internal sealed record SensitiveFlowToolBuildResult(
    bool Started,
    bool TimedOut,
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal sealed class SensitiveFlowToolHost : ISensitiveFlowToolHost
{
    public static SensitiveFlowToolHost Instance { get; } = new();

    public string CurrentDirectory => Directory.GetCurrentDirectory();

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        => Directory.EnumerateFiles(path, searchPattern, searchOption);

    public IEnumerable<string> ReadLines(string path) => File.ReadLines(path);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public Assembly LoadAssembly(string path) => Assembly.LoadFrom(path);

    public SensitiveFlowToolBuildResult Build(string buildTarget, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo("dotnet", $"build \"{buildTarget}\" -c Release --nologo")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return new SensitiveFlowToolBuildResult(false, false, 5, string.Empty, string.Empty);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return new SensitiveFlowToolBuildResult(true, true, 5, string.Empty, string.Empty);
        }

        return new SensitiveFlowToolBuildResult(
            true,
            false,
            process.ExitCode,
            stdoutTask.GetAwaiter().GetResult(),
            stderrTask.GetAwaiter().GetResult());
    }
}
