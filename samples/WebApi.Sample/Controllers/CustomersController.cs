using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Anonymization.Erasure;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Export;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Json.Configuration;
using SensitiveFlow.Json.Converters;
using SensitiveFlow.Json.Extensions;
using SensitiveFlow.Retention.Services;
using WebApi.Sample.Infrastructure;

namespace WebApi.Sample.Controllers;

[ApiController]
[Route("employees")]
public sealed class EmployeesController : ControllerBase
{
    private readonly SampleDbContext _db;
    private readonly IAuditStore _auditStore;
    private readonly IAuditContext _auditContext;
    private readonly IDataSubjectExporter _exporter;
    private readonly IDataSubjectErasureService _erasure;
    private readonly RetentionExecutor _retention;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(
        SampleDbContext db,
        IAuditStore auditStore,
        IAuditContext auditContext,
        IDataSubjectExporter exporter,
        IDataSubjectErasureService erasure,
        RetentionExecutor retention,
        ILogger<EmployeesController> logger)
    {
        _db = db;
        _auditStore = auditStore;
        _auditContext = auditContext;
        _exporter = exporter;
        _erasure = erasure;
        _retention = retention;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var employees = await _db.Employees
            .Take(100)
            .ToListAsync(ct);

        return Ok(employees
            .OrderByDescending(c => c.CreatedAt)
            .Take(20)
            .Select(ToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var employee = new Employee
        {
            DataSubjectId = Guid.NewGuid().ToString(),
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            AnnualSalary = request.AnnualSalary,
            Department = request.Department,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Employee created - name: {Name}, email: {[Sensitive]Email}, salary: {[Sensitive]Salary}",
            employee.FullName.MaskName(),
            employee.Email.MaskEmail(),
            employee.AnnualSalary.ToString().MaskEmail());

        return CreatedAtAction(nameof(Get), new { id = employee.DataSubjectId }, ToResponse(employee));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var employee = await FindEmployeeAsync(id, ct);

        if (employee is null)
        {
            return NotFound();
        }

        await _auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId = employee.DataSubjectId,
            Entity = nameof(Employee),
            Field = "*",
            Operation = AuditOperation.Access,
            ActorId = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
        }, ct);

        _logger.LogInformation("Employee {Id} accessed - email: {[Sensitive]Email}",
            id.SanitizeForLog(), employee.Email.MaskEmail());

        return Ok(ToResponse(employee));
    }

    [HttpGet("{id}/audit")]
    public async Task<IActionResult> GetAudit(string id, CancellationToken ct)
    {
        var employee = await FindEmployeeAsync(id, ct);
        if (employee is null)
        {
            return NotFound();
        }

        var records = await _auditStore.QueryByDataSubjectAsync(employee.DataSubjectId, cancellationToken: ct);
        return Ok(records);
    }

    [HttpGet("{id}/json")]
    public async Task<IActionResult> GetWithJsonRedaction(string id, CancellationToken ct)
    {
        var employee = await FindEmployeeAsync(id, ct);

        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpGet("{id}/role-based")]
    public async Task<IActionResult> GetWithRoleBasedRedaction(string id, CancellationToken ct)
    {
        var employee = await FindEmployeeAsync(id, ct);
        if (employee is null)
        {
            return NotFound();
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Customer";

        var context = userRole switch
        {
            "Admin" => RedactionContext.AdminView,
            "Support" => RedactionContext.SupportView,
            "Customer" => RedactionContext.CustomerView,
            _ => RedactionContext.ApiResponse,
        };

        var options = new JsonSerializerOptions();
        options.WithSensitiveDataRedaction(
            new JsonRedactionOptions { DefaultMode = SensitiveFlow.Json.Enums.JsonRedactionMode.Mask },
            context);

        var json = JsonSerializer.Serialize(employee, options);
        return Ok(JsonSerializer.Deserialize<object>(json));
    }

    [HttpGet("orders/composite")]
    public async Task<IActionResult> GetOrdersWithComposite(CancellationToken ct)
    {
        var orders = await _db.EmployeeOrders
            .Take(10)
            .ToListAsync(ct);

        if (!orders.Any())
        {
            return Ok(new { message = "No orders found. Orders use CompositeDataSubjectId(EmployeeId, OrderId)." });
        }

        return Ok(orders.Select(o => new
        {
            o.EmployeeId,
            o.OrderId,
            DataSubjectKey = $"EmployeeId:{o.EmployeeId};OrderId:{o.OrderId}",
            o.CustomerEmail,
            o.Amount,
        }));
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> Export(string id, CancellationToken ct)
    {
        var employee = await FindEmployeeAsync(id, ct, asNoTracking: true);

        if (employee is null)
        {
            return NotFound();
        }

        await _auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId = employee.DataSubjectId,
            Entity = nameof(Employee),
            Field = "*",
            Operation = AuditOperation.Export,
            ActorId = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
        }, ct);

        return Ok(_exporter.Export(employee));
    }

    [HttpPost("{id}/erase")]
    public async Task<IActionResult> Erase(string id, CancellationToken ct)
    {
        var employee = await FindEmployeeAsync(id, ct);

        if (employee is null)
        {
            return NotFound();
        }

        var changed = _erasure.Erase(employee);
        await _db.SaveChangesAsync(ct);

        await _auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId = employee.DataSubjectId,
            Entity = nameof(Employee),
            Field = "*",
            Operation = AuditOperation.Anonymize,
            ActorId = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
            Details = $"Erased {changed} annotated fields.",
        }, ct);

        return Ok(new { changed });
    }

    [HttpPost("/retention/run")]
    public async Task<IActionResult> RunRetention(CancellationToken ct)
    {
        var employees = await _db.Employees.ToListAsync(ct);
        var report = await _retention.ExecuteAsync(
            employees,
            entity => ((Employee)entity).CreatedAt,
            ct);

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            report.AnonymizedFieldCount,
            report.DeletePendingEntityCount,
            Entries = report.Entries.Select(e => new
            {
                e.FieldName,
                e.ExpiredAt,
                Action = e.Action.ToString(),
            }),
        });
    }

    [HttpPost("/retention/dry-run")]
    public async Task<IActionResult> DryRunRetention(CancellationToken ct)
    {
        var employees = await _db.Employees.AsNoTracking().ToListAsync(ct);
        var report = await _retention.DryRunAsync(
            employees,
            entity => ((Employee)entity).CreatedAt,
            ct);

        return Ok(new
        {
            report.AnonymizedFieldCount,
            report.DeletePendingEntityCount,
            Entries = report.Entries.Select(e => new
            {
                e.FieldName,
                e.ExpiredAt,
                Action = e.Action.ToString(),
            }),
        });
    }

    private static EmployeeResponse ToResponse(Employee e) => new(
        e.DataSubjectId,
        e.FullName.MaskName(),
        e.Email.MaskEmail(),
        e.Phone.MaskPhone(),
        e.AnnualSalary);

    private Task<Employee?> FindEmployeeAsync(string id, CancellationToken ct, bool asNoTracking = false)
    {
        var query = asNoTracking ? _db.Employees.AsNoTracking() : _db.Employees;
        return int.TryParse(id, out var numericId)
            ? query.FirstOrDefaultAsync(c => c.Id == numericId, ct)
            : query.FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);
    }
}

public sealed record CreateEmployeeRequest(
    string FullName,
    string Email,
    string Phone,
    decimal AnnualSalary,
    string Department);

public sealed record EmployeeResponse(
    string DataSubjectId,
    string FullName,
    string Email,
    string Phone,
    decimal AnnualSalary);
