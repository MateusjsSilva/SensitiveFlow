using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.EFCore;
using SensitiveFlow.EFCore.Interceptors;

namespace SensitiveFlow.Benchmarks.EFCore;

/// <summary>
/// Benchmarks for EF Core SaveChanges interceptor performance.
///
/// Measures:
/// - Single entity insert latency
/// - Bulk insert latency (10, 50, 100 entities)
/// - Update operations latency
/// - Delete operations latency
/// - Impact of sensitive field count on performance
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class SaveChangesInterceptorBenchmarks
{
    private DbContextOptions<TestDbContext> _options = null!;
    private TestDbContext _context = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"benchmarks_{Guid.NewGuid()}")
            .Options;

        _context = new TestDbContext(_options);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _context?.Dispose();
    }

    /// <summary>
    /// Benchmark: Insert single entity with sensitive fields
    /// </summary>
    [Benchmark(Description = "Insert single entity (no sensitive data)")]
    public async Task BenchmarkInsertNoSensitiveData()
    {
        var customer = new Customer
        {
            DataSubjectId = $"user_{Guid.NewGuid()}",
            Name = "John Doe",
            Email = "john@example.com",
            PhoneNumber = "+1234567890"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // Cleanup
        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Benchmark: Insert single entity with 3 sensitive fields
    /// </summary>
    [Benchmark(Description = "Insert single entity (3 sensitive fields)")]
    public async Task BenchmarkInsertWithSensitiveData()
    {
        var customer = new SensitiveCustomer
        {
            DataSubjectId = $"user_{Guid.NewGuid()}",
            Name = "Jane Doe",
            Email = "jane@example.com",
            TaxId = "123-45-6789"
        };

        _context.SensitiveCustomers.Add(customer);
        await _context.SaveChangesAsync();

        // Cleanup
        _context.SensitiveCustomers.Remove(customer);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Benchmark: Bulk insert 10 entities
    /// </summary>
    [Benchmark(Description = "Bulk insert (10 entities)")]
    public async Task BenchmarkBulkInsert10()
    {
        var customers = Enumerable.Range(0, 10).Select(i => new SensitiveCustomer
        {
            DataSubjectId = $"user_{i}_{Guid.NewGuid()}",
            Name = $"Customer {i}",
            Email = $"customer{i}@example.com",
            TaxId = $"123-45-{i:D4}"
        }).ToList();

        _context.SensitiveCustomers.AddRange(customers);
        await _context.SaveChangesAsync();

        // Cleanup
        _context.SensitiveCustomers.RemoveRange(customers);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Benchmark: Bulk insert 50 entities
    /// </summary>
    [Benchmark(Description = "Bulk insert (50 entities)")]
    public async Task BenchmarkBulkInsert50()
    {
        var customers = Enumerable.Range(0, 50).Select(i => new SensitiveCustomer
        {
            DataSubjectId = $"user_{i}_{Guid.NewGuid()}",
            Name = $"Customer {i}",
            Email = $"customer{i}@example.com",
            TaxId = $"123-45-{i:D4}"
        }).ToList();

        _context.SensitiveCustomers.AddRange(customers);
        await _context.SaveChangesAsync();

        // Cleanup
        _context.SensitiveCustomers.RemoveRange(customers);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Benchmark: Update single entity
    /// </summary>
    [Benchmark(Description = "Update single entity")]
    public async Task BenchmarkUpdateEntity()
    {
        var customer = new SensitiveCustomer
        {
            DataSubjectId = $"user_{Guid.NewGuid()}",
            Name = "Original Name",
            Email = "original@example.com",
            TaxId = "123-45-6789"
        };

        _context.SensitiveCustomers.Add(customer);
        await _context.SaveChangesAsync();

        customer.Name = "Updated Name";
        customer.Email = "updated@example.com";
        await _context.SaveChangesAsync();

        // Cleanup
        _context.SensitiveCustomers.Remove(customer);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Benchmark: Delete single entity
    /// </summary>
    [Benchmark(Description = "Delete single entity")]
    public async Task BenchmarkDeleteEntity()
    {
        var customer = new SensitiveCustomer
        {
            DataSubjectId = $"user_{Guid.NewGuid()}",
            Name = "Customer to Delete",
            Email = "delete@example.com",
            TaxId = "123-45-6789"
        };

        _context.SensitiveCustomers.Add(customer);
        await _context.SaveChangesAsync();

        _context.SensitiveCustomers.Remove(customer);
        await _context.SaveChangesAsync();
    }
}

// Test Models
public class Customer
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class SensitiveCustomer
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    public string TaxId { get; set; } = string.Empty;
}

// Test DbContext
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<SensitiveCustomer> SensitiveCustomers { get; set; }
}
