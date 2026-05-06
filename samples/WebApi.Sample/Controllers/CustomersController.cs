using Microsoft.AspNetCore.Mvc;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Masking;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace WebApi.Sample.Controllers;

[ApiController]
[Route("customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly IAuditStore _auditStore;
    private readonly IAuditContext _auditContext;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        IAuditStore auditStore,
        IAuditContext auditContext,
        ILogger<CustomersController> logger)
    {
        _auditStore   = auditStore;
        _auditContext = auditContext;
        _logger       = logger;
    }

    /// <summary>
    /// Returns a masked view of the customer — raw PII never leaves the API.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        // Simulate loading from a real store.
        var customer = new Customer
        {
            Id            = id,
            DataSubjectId = id,
            Name          = "Joao da Silva",
            Email         = "joao.silva@example.com",
            TaxId         = "123.456.789-09",
            Phone         = "+55 11 99999-8877",
        };

        // Emit an audit record — actor and IP token come from HttpAuditContext.
        await _auditStore.AppendAsync(new AuditRecord
        {
            DataSubjectId  = customer.DataSubjectId,
            Entity         = nameof(Customer),
            Field          = "*",
            Operation      = AuditOperation.Access,
            ActorId        = _auditContext.ActorId,
            IpAddressToken = _auditContext.IpAddressToken,
        }, ct);

        // The redacting logger strips [PersonalData] values before they reach any sink.
        // SF0001: the analyzer would flag customer.Email passed directly without masking.
        _logger.LogInformation("Customer {Id} accessed — email masked: {Email}",
            customer.Id,
            customer.Email.MaskEmail());

        // Return a masked DTO — raw PII never leaves the API boundary.
        // SF0002: the analyzer would flag returning customer.Email directly via Ok().
        return Ok(new CustomerResponse(
            customer.Id,
            customer.Name.MaskName(),
            customer.Email.MaskEmail(),
            customer.Phone.MaskPhone()));
    }

    /// <summary>
    /// Returns the audit trail for a data subject.
    /// </summary>
    [HttpGet("{id}/audit")]
    public async Task<IActionResult> GetAudit(string id, CancellationToken ct)
    {
        var records = await _auditStore.QueryByDataSubjectAsync(id, cancellationToken: ct);
        return Ok(records);
    }
}

// -----------------------------------------------------------------------
// Model
// -----------------------------------------------------------------------

public sealed class Customer
{
    public string Id { get; set; } = string.Empty;
    public string DataSubjectId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Identification)]
    public string Name { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Email { get; set; } = string.Empty;

    [SensitiveData(Category = SensitiveDataCategory.Other)]
    [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
    public string TaxId { get; set; } = string.Empty;

    [PersonalData(Category = DataCategory.Contact)]
    public string Phone { get; set; } = string.Empty;
}

// Response DTO — contains only masked values, safe to serialize and log.
public sealed record CustomerResponse(string Id, string Name, string Email, string Phone);
