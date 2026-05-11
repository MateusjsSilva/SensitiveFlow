using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Anonymization.Erasure;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Export;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Retention.Services;
using WebApi.Sample.Infrastructure;

namespace WebApi.Sample.Controllers;

[ApiController]
[Route("customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly SampleDbContext _db;
    private readonly IAuditStore _auditStore;
    private readonly IAuditContext _auditContext;
    private readonly IDataSubjectExporter _exporter;
    private readonly IDataSubjectErasureService _erasure;
    private readonly RetentionExecutor _retention;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        SampleDbContext db,
        IAuditStore auditStore,
        IAuditContext auditContext,
        IDataSubjectExporter exporter,
        IDataSubjectErasureService erasure,
        RetentionExecutor retention,
        ILogger<CustomersController> logger)
    {
        _db = db;
        _auditStore = auditStore;
        _auditContext = auditContext;
        _exporter = exporter;
        _erasure = erasure;
        _retention = retention;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = new Customer
        {
            DataSubjectId = Guid.NewGuid().ToString(),
            Name = request.Name,
            Email = request.Email,
            TaxId = request.TaxId,
            Phone = request.Phone,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Customer created - name: {Name}, email: {[Sensitive]Email}",
            customer.Name.MaskName(),
            customer.Email);

        return CreatedAtAction(nameof(Get), new { id = customer.DataSubjectId }, ToResponse(customer));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

        if (customer is null)
        {
            return NotFound();
        }

        await _auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId = customer.DataSubjectId,
            Entity = nameof(Customer),
            Field = "*",
            Operation = AuditOperation.Access,
            ActorId = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
        }, ct);

        _logger.LogInformation("Customer {Id} accessed - email: {[Sensitive]Email}",
            id, customer.Email);

        return Ok(ToResponse(customer));
    }

    [HttpGet("{id}/audit")]
    public async Task<IActionResult> GetAudit(string id, CancellationToken ct)
    {
        var records = await _auditStore.QueryByDataSubjectAsync(id, cancellationToken: ct);
        return Ok(records);
    }

    [HttpGet("{id}/json")]
    public async Task<IActionResult> GetWithJsonRedaction(string id, CancellationToken ct)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> Export(string id, CancellationToken ct)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

        if (customer is null)
        {
            return NotFound();
        }

        await _auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId = customer.DataSubjectId,
            Entity = nameof(Customer),
            Field = "*",
            Operation = AuditOperation.Export,
            ActorId = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
        }, ct);

        return Ok(_exporter.Export(customer));
    }

    [HttpPost("{id}/erase")]
    public async Task<IActionResult> Erase(string id, CancellationToken ct)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

        if (customer is null)
        {
            return NotFound();
        }

        var changed = _erasure.Erase(customer);
        await _db.SaveChangesAsync(ct);

        await _auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId = customer.DataSubjectId,
            Entity = nameof(Customer),
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
        var customers = await _db.Customers.ToListAsync(ct);
        var report = await _retention.ExecuteAsync(
            customers,
            entity => ((Customer)entity).CreatedAt,
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
        var customers = await _db.Customers.AsNoTracking().ToListAsync(ct);
        var report = await _retention.DryRunAsync(
            customers,
            entity => ((Customer)entity).CreatedAt,
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

    private static CustomerResponse ToResponse(Customer c) => new(
        c.DataSubjectId,
        c.Name.MaskName(),
        c.Email.MaskEmail(),
        c.Phone.MaskPhone());
}

public sealed record CreateCustomerRequest(
    string Name,
    string Email,
    string TaxId,
    string Phone);

public sealed record CustomerResponse(
    string DataSubjectId,
    string Name,
    string Email,
    string Phone);
