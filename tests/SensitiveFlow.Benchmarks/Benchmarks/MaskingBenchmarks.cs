using BenchmarkDotNet.Attributes;
using SensitiveFlow.Anonymization.Anonymizers;
using SensitiveFlow.Anonymization.Masking;

namespace SensitiveFlow.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class MaskingBenchmarks
{
    private static readonly EmailMasker EmailMasker = new();
    private static readonly PhoneMasker PhoneMasker = new();
    private static readonly NameMasker  NameMasker  = new();
    private static readonly BrazilianTaxIdAnonymizer TaxIdAnonymizer = new();

    private const string Email = "joao.silva@example.com";
    private const string Phone = "+55 11 99999-8877";
    private const string Name  = "João da Silva Sauro";
    private const string Cpf   = "123.456.789-09";

    [Benchmark]
    public string MaskEmail() => EmailMasker.Mask(Email);

    [Benchmark]
    public string MaskPhone() => PhoneMasker.Mask(Phone);

    [Benchmark]
    public string MaskName() => NameMasker.Mask(Name);

    [Benchmark]
    public string AnonymizeCpf() => TaxIdAnonymizer.Anonymize(Cpf);
}
