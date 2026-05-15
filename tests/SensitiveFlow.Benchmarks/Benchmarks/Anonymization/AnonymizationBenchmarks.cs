using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SensitiveFlow.Anonymization.Strategies;

namespace SensitiveFlow.Benchmarks.Anonymization;

/// <summary>
/// Benchmarks for anonymization and masking operations performance.
///
/// Measures:
/// - Email masking latency
/// - Phone number masking latency
/// - Name masking latency
/// - Fingerprint generation latency
/// - Deterministic vs. random masking
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class AnonymizationBenchmarks
{
    private readonly List<string> _emails = new();
    private readonly List<string> _phoneNumbers = new();
    private readonly List<string> _names = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Generate test data
        for (int i = 0; i < 100; i++)
        {
            _emails.Add($"user{i}@example.com");
            _phoneNumbers.Add($"+1{20000000000 + i}");
            _names.Add($"Customer {i} Name");
        }
    }

    /// <summary>
    /// Benchmark: Mask email address
    /// </summary>
    [Benchmark(Description = "Mask email")]
    public string BenchmarkMaskEmail()
    {
        var email = _emails[Random.Shared.Next(_emails.Count)];
        return MaskEmail(email);
    }

    /// <summary>
    /// Benchmark: Mask phone number
    /// </summary>
    [Benchmark(Description = "Mask phone number")]
    public string BenchmarkMaskPhone()
    {
        var phone = _phoneNumbers[Random.Shared.Next(_phoneNumbers.Count)];
        return MaskPhone(phone);
    }

    /// <summary>
    /// Benchmark: Mask name
    /// </summary>
    [Benchmark(Description = "Mask name")]
    public string BenchmarkMaskName()
    {
        var name = _names[Random.Shared.Next(_names.Count)];
        return MaskName(name);
    }

    /// <summary>
    /// Benchmark: Generate deterministic fingerprint (for deduplication)
    /// </summary>
    [Benchmark(Description = "Generate fingerprint (SHA256)")]
    public string BenchmarkGenerateFingerprint()
    {
        var email = _emails[Random.Shared.Next(_emails.Count)];
        return GenerateFingerprint(email);
    }

    /// <summary>
    /// Benchmark: Mask 10 emails in sequence
    /// </summary>
    [Benchmark(Description = "Mask 10 emails sequentially")]
    public List<string> BenchmarkMask10EmailsSequential()
    {
        var results = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var email = _emails[i % _emails.Count];
            results.Add(MaskEmail(email));
        }
        return results;
    }

    /// <summary>
    /// Benchmark: Mask 10 emails in parallel
    /// </summary>
    [Benchmark(Description = "Mask 10 emails in parallel")]
    public List<string> BenchmarkMask10EmailsParallel()
    {
        var tasks = Enumerable.Range(0, 10).Select(i =>
            Task.Run(() => MaskEmail(_emails[i % _emails.Count]))
        ).ToList();

        Task.WaitAll(tasks.ToArray());
        return tasks.Select(t => t.Result).ToList();
    }

    /// <summary>
    /// Benchmark: Combine multiple masking operations
    /// </summary>
    [Benchmark(Description = "Mask email + phone + name")]
    public (string, string, string) BenchmarkMaskMultiple()
    {
        var email = _emails[Random.Shared.Next(_emails.Count)];
        var phone = _phoneNumbers[Random.Shared.Next(_phoneNumbers.Count)];
        var name = _names[Random.Shared.Next(_names.Count)];

        return (
            MaskEmail(email),
            MaskPhone(phone),
            MaskName(name)
        );
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            return "[REDACTED]";

        var parts = email.Split('@');
        var localPart = parts[0];
        var domain = parts[1];

        if (localPart.Length <= 2)
            return $"*@{domain}";

        return $"{localPart[0]}***@{domain}";
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 4)
            return "[REDACTED]";

        return $"***-***-{phone[^4..]}";
    }

    private static string MaskName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "[REDACTED]";

        var parts = name.Split(' ');
        if (parts.Length == 0)
            return "[REDACTED]";

        var firstPart = parts[0];
        var lastName = parts.Length > 1 ? parts[^1] : string.Empty;

        if (firstPart.Length > 0 && lastName.Length > 0)
            return $"{firstPart[0]}*** {lastName[0]}***";

        if (firstPart.Length > 0)
            return $"{firstPart[0]}***";

        return "[REDACTED]";
    }

    private static string GenerateFingerprint(string value)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashBytes);
    }
}
