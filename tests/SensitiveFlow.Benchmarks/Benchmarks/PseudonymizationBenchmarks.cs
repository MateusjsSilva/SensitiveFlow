using BenchmarkDotNet.Attributes;
using SensitiveFlow.Anonymization.Pseudonymizers;

namespace SensitiveFlow.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class PseudonymizationBenchmarks
{
    private const string SecretKey = "benchmark-secret-key-32-bytes!!!!";
    private const string Value = "joao.silva@example.com";

    private readonly HmacPseudonymizer _hmac = new(SecretKey);

    [Benchmark(Baseline = true)]
    public string Hmac_Pseudonymize() => _hmac.Pseudonymize(Value);

    [Benchmark]
    public string Hmac_PseudonymizeFreshInstance() => new HmacPseudonymizer(SecretKey).Pseudonymize(Value);
}
