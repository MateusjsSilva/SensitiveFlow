using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Logging;

namespace SensitiveFlow.Benchmarks.Logging;

/// <summary>
/// Benchmarks for logging redaction performance.
///
/// Measures:
/// - Single log message latency (various levels)
/// - Structured logging with sensitive values
/// - Throughput of log messages per second
/// - Impact of redaction on logging overhead
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class LoggingRedactionBenchmarks
{
    private ILogger _logger = null!;
    private ILogger _redactedLogger = null!;
    private readonly List<string> _sensitiveValues = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Setup logging
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        _logger = services.GetRequiredService<ILogger<LoggingRedactionBenchmarks>>();
        _redactedLogger = services.GetRequiredService<ILogger<LoggingRedactionBenchmarks>>();

        // Generate test data
        for (int i = 0; i < 100; i++)
        {
            _sensitiveValues.Add($"user{i}@example.com");
        }
    }

    /// <summary>
    /// Benchmark: Log single Information level message without sensitive data
    /// </summary>
    [Benchmark(Description = "LogInformation (no sensitive data)")]
    public void BenchmarkLogInformationNoSensitive()
    {
        _logger.LogInformation("Processing request for customer {CustomerId}", Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Benchmark: Log single Information level message with sensitive email
    /// </summary>
    [Benchmark(Description = "LogInformation (with email)")]
    public void BenchmarkLogInformationWithEmail()
    {
        var email = _sensitiveValues[Random.Shared.Next(_sensitiveValues.Count)];
        _logger.LogInformation("User email: {Email}", email);
    }

    /// <summary>
    /// Benchmark: Redacted log with sensitive email
    /// </summary>
    [Benchmark(Description = "Redacted LogInformation (with email)")]
    public void BenchmarkRedactedLogInformationWithEmail()
    {
        var email = _sensitiveValues[Random.Shared.Next(_sensitiveValues.Count)];
        _redactedLogger.LogInformation("User email: {Email}", email);
    }

    /// <summary>
    /// Benchmark: Log with multiple structured properties
    /// </summary>
    [Benchmark(Description = "LogInformation (5 properties)")]
    public void BenchmarkLogMultipleProperties()
    {
        var email = _sensitiveValues[Random.Shared.Next(_sensitiveValues.Count)];
        _logger.LogInformation(
            "Customer created: {Email}, {Name}, {Phone}, {Address}, {Id}",
            email,
            "John Doe",
            "+1234567890",
            "123 Main St",
            Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Benchmark: Redacted log with multiple structured properties
    /// </summary>
    [Benchmark(Description = "Redacted LogInformation (5 properties)")]
    public void BenchmarkRedactedLogMultipleProperties()
    {
        var email = _sensitiveValues[Random.Shared.Next(_sensitiveValues.Count)];
        _redactedLogger.LogInformation(
            "Customer created: {Email}, {Name}, {Phone}, {Address}, {Id}",
            email,
            "John Doe",
            "+1234567890",
            "123 Main St",
            Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Benchmark: Log Warning level message
    /// </summary>
    [Benchmark(Description = "LogWarning")]
    public void BenchmarkLogWarning()
    {
        _logger.LogWarning("Unusual activity detected for {Email}", _sensitiveValues[0]);
    }

    /// <summary>
    /// Benchmark: Log Error level message with exception
    /// </summary>
    [Benchmark(Description = "LogError with exception")]
    public void BenchmarkLogError()
    {
        var ex = new InvalidOperationException("Something went wrong");
        _logger.LogError(ex, "Error processing user {Email}", _sensitiveValues[0]);
    }

    /// <summary>
    /// Benchmark: Log Critical level message
    /// </summary>
    [Benchmark(Description = "LogCritical")]
    public void BenchmarkLogCritical()
    {
        _logger.LogCritical("Security breach detected for {Email}", _sensitiveValues[0]);
    }

    /// <summary>
    /// Benchmark: Burst of 10 log messages (simulates rapid logging)
    /// </summary>
    [Benchmark(Description = "Burst logging (10 messages)")]
    public void BenchmarkBurstLogging()
    {
        for (int i = 0; i < 10; i++)
        {
            var email = _sensitiveValues[i % _sensitiveValues.Count];
            _logger.LogInformation("Processing user {Email} request {RequestId}", email, Guid.NewGuid());
        }
    }

    /// <summary>
    /// Benchmark: Burst of 10 redacted log messages
    /// </summary>
    [Benchmark(Description = "Redacted burst logging (10 messages)")]
    public void BenchmarkRedactedBurstLogging()
    {
        for (int i = 0; i < 10; i++)
        {
            var email = _sensitiveValues[i % _sensitiveValues.Count];
            _redactedLogger.LogInformation("Processing user {Email} request {RequestId}", email, Guid.NewGuid());
        }
    }
}
