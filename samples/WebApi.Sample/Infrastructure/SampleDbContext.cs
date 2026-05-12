using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

namespace WebApi.Sample.Infrastructure;

public sealed class Employee
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    public string FullName { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Phone { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [RetentionData(Years = 7, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public decimal AnnualSalary { get; set; }

    [SensitiveData(Category = SensitiveDataCategory.Other)]
    public string Department { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
}
