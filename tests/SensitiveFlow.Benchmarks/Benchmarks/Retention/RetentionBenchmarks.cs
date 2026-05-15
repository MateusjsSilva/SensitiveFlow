using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using System.Reflection;

namespace SensitiveFlow.Benchmarks.Retention;

/// <summary>
/// Benchmarks for retention and data expiration policies performance.
///
/// Measures:
/// - Retention metadata discovery latency
/// - Expiration check performance
/// - Field scanning for retention attributes
/// - Policy evaluation latency
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class RetentionBenchmarks
{
    private Type _testType = null!;
    private readonly List<object> _testObjects = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        _testType = typeof(RetentionTestEntity);

        // Generate test objects
        for (int i = 0; i < 100; i++)
        {
            _testObjects.Add(new RetentionTestEntity
            {
                Id = i,
                DataSubjectId = $"user_{i}",
                TransactionId = Guid.NewGuid().ToString(),
                CustomerName = $"Customer {i}",
                TaxId = $"123-45-{i:D4}",
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(365))
            });
        }
    }

    /// <summary>
    /// Benchmark: Discover retention attributes on a type
    /// </summary>
    [Benchmark(Description = "Discover retention attributes")]
    public List<(PropertyInfo, RetentionDataAttribute)> BenchmarkDiscoverRetentionAttributes()
    {
        var result = new List<(PropertyInfo, RetentionDataAttribute)>();

        foreach (var property in _testType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = property.GetCustomAttribute<RetentionDataAttribute>();
            if (attr != null)
            {
                result.Add((property, attr));
            }
        }

        return result;
    }

    /// <summary>
    /// Benchmark: Check if field is expired based on retention policy
    /// </summary>
    [Benchmark(Description = "Check field expiration")]
    public bool BenchmarkCheckFieldExpiration()
    {
        var entity = (RetentionTestEntity)_testObjects[0];
        var createdDate = entity.CreatedAt;
        var retentionYears = 5;
        var expirationDate = createdDate.AddYears(retentionYears);

        return DateTime.UtcNow > expirationDate;
    }

    /// <summary>
    /// Benchmark: Evaluate retention policy for entity
    /// </summary>
    [Benchmark(Description = "Evaluate retention policy")]
    public bool BenchmarkEvaluateRetentionPolicy()
    {
        var entity = (RetentionTestEntity)_testObjects[0];
        var createdDate = entity.CreatedAt;
        var retentionYears = 5;
        var expirationDate = createdDate.AddYears(retentionYears);
        var isExpired = DateTime.UtcNow > expirationDate;

        return isExpired;
    }

    /// <summary>
    /// Benchmark: Scan entity for all retention policies
    /// </summary>
    [Benchmark(Description = "Scan entity for retention")]
    public List<RetentionInfo> BenchmarkScanEntityRetention()
    {
        var result = new List<RetentionInfo>();

        foreach (var property in _testType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = property.GetCustomAttribute<RetentionDataAttribute>();
            if (attr != null)
            {
                result.Add(new RetentionInfo
                {
                    PropertyName = property.Name,
                    RetentionYears = attr.Years,
                    Policy = attr.Policy
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Benchmark: Calculate retention period for batch of 10 entities
    /// </summary>
    [Benchmark(Description = "Calculate retention (10 entities)")]
    public List<ExpirationStatus> BenchmarkCalculateRetention10()
    {
        var result = new List<ExpirationStatus>();

        for (int i = 0; i < 10; i++)
        {
            var entity = (RetentionTestEntity)_testObjects[i];
            var expirationDate = entity.CreatedAt.AddYears(5);
            var isExpired = DateTime.UtcNow > expirationDate;
            var daysUntilExpiration = (expirationDate - DateTime.UtcNow).Days;

            result.Add(new ExpirationStatus
            {
                EntityId = entity.Id,
                IsExpired = isExpired,
                DaysUntilExpiration = daysUntilExpiration
            });
        }

        return result;
    }

    /// <summary>
    /// Benchmark: Calculate retention period for batch of 50 entities
    /// </summary>
    [Benchmark(Description = "Calculate retention (50 entities)")]
    public List<ExpirationStatus> BenchmarkCalculateRetention50()
    {
        var result = new List<ExpirationStatus>();

        for (int i = 0; i < 50; i++)
        {
            var entity = (RetentionTestEntity)_testObjects[i];
            var expirationDate = entity.CreatedAt.AddYears(5);
            var isExpired = DateTime.UtcNow > expirationDate;
            var daysUntilExpiration = (expirationDate - DateTime.UtcNow).Days;

            result.Add(new ExpirationStatus
            {
                EntityId = entity.Id,
                IsExpired = isExpired,
                DaysUntilExpiration = daysUntilExpiration
            });
        }

        return result;
    }

    /// <summary>
    /// Benchmark: Identify fields needing anonymization
    /// </summary>
    [Benchmark(Description = "Identify fields for anonymization")]
    public List<string> BenchmarkIdentifyFieldsForAnonymization()
    {
        var result = new List<string>();

        foreach (var property in _testType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var retentionAttr = property.GetCustomAttribute<RetentionDataAttribute>();
            if (retentionAttr?.Policy == RetentionPolicy.AnonymizeOnExpiration)
            {
                result.Add(property.Name);
            }
        }

        return result;
    }
}

// Test Models
public class RetentionTestEntity
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string CustomerName { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [RetentionData(Years = 7, Policy = RetentionPolicy.DeleteOnExpiration)]
    public string TaxId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class RetentionInfo
{
    public string PropertyName { get; set; } = string.Empty;
    public int RetentionYears { get; set; }
    public RetentionPolicy Policy { get; set; }
}

public class ExpirationStatus
{
    public int EntityId { get; set; }
    public bool IsExpired { get; set; }
    public int DaysUntilExpiration { get; set; }
}
