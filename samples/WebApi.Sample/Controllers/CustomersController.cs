using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Masking;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using WebApi.Sample.Infrastructure;

namespace WebApi.Sample.Controllers;

[ApiController]
[Route("customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly SampleDbContext _db;
    private readonly IAuditStore _auditStore;
    private readonly IAuditContext _auditContext;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        SampleDbContext db,
        IAuditStore auditStore,
        IAuditContext auditContext,
        ILogger<CustomersController> logger)
    {
        _db           = db;
        _auditStore   = auditStore;
        _auditContext = auditContext;
        _logger       = logger;
    }

    /// <summary>
    /// Creates a new customer. EF Core interceptor emits audit records automatically on SaveChanges.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = new Customer
        {
            DataSubjectId = Guid.NewGuid().ToString(),
            Name          = request.Name,
            Email         = request.Email,
            TaxId         = request.TaxId,
            Phone         = request.Phone,
            CreatedAt     = DateTimeOffset.UtcNow,
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Customer created — name: {Name}, email: {[Sensitive]Email}",
            customer.Name.MaskName(),
            customer.Email);

        return CreatedAtAction(nameof(Get), new { id = customer.DataSubjectId },
            ToResponse(customer));
    }

    /// <summary>
    /// Returns a masked view of the customer. Raw PII never leaves the API.
    /// </summary>
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
            DataSubjectId  = customer.DataSubjectId,
            Entity         = nameof(Customer),
            Field          = "*",
            Operation      = AuditOperation.Access,
            ActorId        = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
        }, ct);

        _logger.LogInformation("Customer {Id} accessed — email: {[Sensitive]Email}",
            id, customer.Email);

        return Ok(ToResponse(customer));
    }

    /// <summary>
    /// Returns the full audit trail for a data subject.
    /// </summary>
    [HttpGet("{id}/audit")]
    public async Task<IActionResult> GetAudit(string id, CancellationToken ct)
    {
        var records = await _auditStore.QueryByDataSubjectAsync(id, cancellationToken: ct);
        return Ok(records);
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
