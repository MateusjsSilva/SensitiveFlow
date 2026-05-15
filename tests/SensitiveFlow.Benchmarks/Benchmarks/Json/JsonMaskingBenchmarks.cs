using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using System.Text.Json;

namespace SensitiveFlow.Benchmarks.Json;

/// <summary>
/// Benchmarks for JSON masking and redaction performance.
///
/// Measures:
/// - Serialization latency with masking
/// - Deserialization performance
/// - Impact of field count on performance
/// - Memory allocation during serialization
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class JsonMaskingBenchmarks
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions OptionsIndented = new() { WriteIndented = true };

    /// <summary>
    /// Benchmark: Serialize simple object without sensitive data
    /// </summary>
    [Benchmark(Description = "Serialize simple object (no sensitive)")]
    public string BenchmarkSerializeSimple()
    {
        var customer = new SimpleCustomer
        {
            Id = 1,
            Name = "John Doe",
            Active = true
        };

        return JsonSerializer.Serialize(customer, Options);
    }

    /// <summary>
    /// Benchmark: Serialize object with 2 sensitive fields
    /// </summary>
    [Benchmark(Description = "Serialize object (2 sensitive fields)")]
    public string BenchmarkSerializeWithSensitive()
    {
        var customer = new SensitiveCustomerDto
        {
            Id = 1,
            Name = "Jane Doe",
            Email = "jane.doe@example.com",
            Active = true
        };

        return JsonSerializer.Serialize(customer, Options);
    }

    /// <summary>
    /// Benchmark: Serialize object with 5 sensitive fields
    /// </summary>
    [Benchmark(Description = "Serialize object (5 sensitive fields)")]
    public string BenchmarkSerializeMultipleSensitive()
    {
        var customer = new DetailedCustomerDto
        {
            Id = 1,
            Name = "John Smith",
            Email = "john.smith@example.com",
            PhoneNumber = "+1234567890",
            Address = "123 Main Street",
            TaxId = "123-45-6789"
        };

        return JsonSerializer.Serialize(customer, Options);
    }

    /// <summary>
    /// Benchmark: Serialize with indentation (API response style)
    /// </summary>
    [Benchmark(Description = "Serialize indented response")]
    public string BenchmarkSerializeIndented()
    {
        var customer = new SensitiveCustomerDto
        {
            Id = 1,
            Name = "Jane Doe",
            Email = "jane.doe@example.com",
            Active = true
        };

        return JsonSerializer.Serialize(customer, OptionsIndented);
    }

    /// <summary>
    /// Benchmark: Serialize array of 10 objects
    /// </summary>
    [Benchmark(Description = "Serialize array (10 objects)")]
    public string BenchmarkSerializeArray10()
    {
        var customers = Enumerable.Range(0, 10).Select(i => new SensitiveCustomerDto
        {
            Id = i,
            Name = $"Customer {i}",
            Email = $"customer{i}@example.com",
            Active = i % 2 == 0
        }).ToList();

        return JsonSerializer.Serialize(customers, Options);
    }

    /// <summary>
    /// Benchmark: Serialize array of 50 objects
    /// </summary>
    [Benchmark(Description = "Serialize array (50 objects)")]
    public string BenchmarkSerializeArray50()
    {
        var customers = Enumerable.Range(0, 50).Select(i => new SensitiveCustomerDto
        {
            Id = i,
            Name = $"Customer {i}",
            Email = $"customer{i}@example.com",
            Active = i % 2 == 0
        }).ToList();

        return JsonSerializer.Serialize(customers, Options);
    }

    /// <summary>
    /// Benchmark: Deserialize simple JSON
    /// </summary>
    [Benchmark(Description = "Deserialize simple object")]
    public SimpleCustomer? BenchmarkDeserializeSimple()
    {
        const string json = """{"id":1,"name":"John Doe","active":true}""";
        return JsonSerializer.Deserialize<SimpleCustomer>(json, Options);
    }

    /// <summary>
    /// Benchmark: Deserialize object with sensitive fields
    /// </summary>
    [Benchmark(Description = "Deserialize object (sensitive fields)")]
    public SensitiveCustomerDto? BenchmarkDeserializeSensitive()
    {
        const string json = """{"id":1,"name":"Jane Doe","email":"jane@example.com","active":true}""";
        return JsonSerializer.Deserialize<SensitiveCustomerDto>(json, Options);
    }

    /// <summary>
    /// Benchmark: Round-trip serialize and deserialize
    /// </summary>
    [Benchmark(Description = "Round-trip (serialize + deserialize)")]
    public SensitiveCustomerDto? BenchmarkRoundTrip()
    {
        var customer = new SensitiveCustomerDto
        {
            Id = 1,
            Name = "Jane Doe",
            Email = "jane.doe@example.com",
            Active = true
        };

        var json = JsonSerializer.Serialize(customer, Options);
        return JsonSerializer.Deserialize<SensitiveCustomerDto>(json, Options);
    }

    /// <summary>
    /// Benchmark: Serialize nested objects
    /// </summary>
    [Benchmark(Description = "Serialize nested objects")]
    public string BenchmarkSerializeNested()
    {
        var order = new OrderDto
        {
            Id = 1,
            OrderNumber = "ORD-001",
            Customer = new SensitiveCustomerDto
            {
                Id = 1,
                Name = "Jane Doe",
                Email = "jane@example.com",
                Active = true
            },
            Total = 99.99m
        };

        return JsonSerializer.Serialize(order, Options);
    }
}

// Test Models
public class SimpleCustomer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
}

public class SensitiveCustomerDto
{
    public int Id { get; set; }

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    public bool Active { get; set; }
}

public class DetailedCustomerDto
{
    public int Id { get; set; }

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string PhoneNumber { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Address { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    public string TaxId { get; set; } = string.Empty;
}

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public SensitiveCustomerDto? Customer { get; set; }
    public decimal Total { get; set; }
}
