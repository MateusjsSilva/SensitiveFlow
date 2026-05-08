using BenchmarkDotNet.Attributes;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.Benchmarks.Benchmarks;

/// <summary>
/// Compares the SaveChanges-time hot path (per-entity attribute scan) against the cached
/// resolution provided by <see cref="SensitiveMemberCache"/>.
/// </summary>
[MemoryDiagnoser]
public class InterceptorReflectionBenchmarks
{
    private readonly TestEntity _entity = new();
    private readonly Type _type = typeof(TestEntity);

    [Benchmark(Baseline = true, Description = "Reflection on every call (pre-cache behavior)")]
    public int Reflection_NoCache()
    {
        var count = 0;
        foreach (var property in _type.GetProperties())
        {
            if (Attribute.IsDefined(property, typeof(PersonalDataAttribute)) ||
                Attribute.IsDefined(property, typeof(SensitiveDataAttribute)))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Cached lookup via SensitiveMemberCache")]
    public int Reflection_Cached() =>
        SensitiveMemberCache.GetSensitiveProperties(_type).Count;

    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string DataSubjectId { get; set; } = "subject-x";

        [PersonalData(Category = DataCategory.Identification)]
        public string Name { get; set; } = string.Empty;

        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = string.Empty;

        [SensitiveData(Category = SensitiveDataCategory.Health)]
        public string HealthNote { get; set; } = string.Empty;

        public string PublicField { get; set; } = string.Empty;
    }
}
