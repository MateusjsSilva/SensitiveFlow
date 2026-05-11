using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

namespace QuickStart.Sample;

public sealed class Customer
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Other)]
    public string TaxId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
}
