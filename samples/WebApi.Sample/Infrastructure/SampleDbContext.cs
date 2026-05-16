using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;

namespace WebApi.Sample.Infrastructure;

public sealed class Employee
{
    public int Id { get; set; }
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    [Redaction(
        ApiResponse = OutputRedactionAction.Mask,
        AdminView = OutputRedactionAction.None,
        SupportView = OutputRedactionAction.Mask,
        CustomerView = OutputRedactionAction.Redact,
        Logs = OutputRedactionAction.Redact,
        Audit = OutputRedactionAction.Mask)]
    public string FullName { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    [Redaction(
        ApiResponse = OutputRedactionAction.Mask,
        AdminView = OutputRedactionAction.None,
        SupportView = OutputRedactionAction.Mask,
        CustomerView = OutputRedactionAction.Redact,
        Logs = OutputRedactionAction.Redact,
        Audit = OutputRedactionAction.Mask)]
    public string Email { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    [Redaction(
        ApiResponse = OutputRedactionAction.Mask,
        AdminView = OutputRedactionAction.None,
        SupportView = OutputRedactionAction.Redact,
        CustomerView = OutputRedactionAction.Redact,
        Logs = OutputRedactionAction.Redact)]
    public string Phone { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [RetentionData(Years = 7, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    [Redaction(
        ApiResponse = OutputRedactionAction.Omit,
        AdminView = OutputRedactionAction.None,
        SupportView = OutputRedactionAction.Redact,
        CustomerView = OutputRedactionAction.Omit,
        Logs = OutputRedactionAction.Redact)]
    public decimal AnnualSalary { get; set; }

    [SensitiveData(Category = SensitiveDataCategory.Other)]
    public string Department { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

[CompositeDataSubjectId(nameof(EmployeeId), nameof(OrderId))]
public sealed class EmployeeOrder
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    [Redaction(
        ApiResponse = OutputRedactionAction.Mask,
        AdminView = OutputRedactionAction.None,
        SupportView = OutputRedactionAction.Mask,
        CustomerView = OutputRedactionAction.Redact,
        Logs = OutputRedactionAction.Redact)]
    public string CustomerEmail { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Financial)]
    [Redaction(
        ApiResponse = OutputRedactionAction.Mask,
        AdminView = OutputRedactionAction.None,
        SupportView = OutputRedactionAction.Mask,
        CustomerView = OutputRedactionAction.Redact,
        Logs = OutputRedactionAction.Redact)]
    public decimal Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeOrder> EmployeeOrders => Set<EmployeeOrder>();
}
