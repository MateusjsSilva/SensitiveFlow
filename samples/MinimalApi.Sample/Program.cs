// ---------------------------------------------
// SensitiveFlow - Minimal API Sample
//
// Demonstrates how SensitiveFlow integrates into a Minimal API application:
//   - [PersonalData] / [SensitiveData] on model properties
//   - Middleware that pseudonymizes the remote IP before it reaches any handler
//   - IAuditStore receiving AuditRecords from IAuditContext
//   - ILogger redaction stripping sensitive values from structured logs
//   - Masking in responses so raw PII never leaves the API boundary
//
// IMPORTANT: Both stubs below (NullAuditStore, NullTokenStore) discard data.
// In production replace them with durable implementations:
//   builder.Services.AddAuditStore<YourEfCoreAuditStore>();
//   builder.Services.AddTokenStore<YourEfCoreTokenStore>();
// ---------------------------------------------

using Microsoft.Extensions.Logging;
using SensitiveFlow.Anonymization.Extensions;
using SensitiveFlow.Anonymization.Masking;
using SensitiveFlow.Audit.Extensions;
using SensitiveFlow.AspNetCore.Extensions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.EFCore.Extensions;
using SensitiveFlow.Logging.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// --- SensitiveFlow: audit store (replace with a durable store in production) ---
builder.Services.AddAuditStore<NullAuditStore>();

// --- SensitiveFlow: token store + pseudonymizer (replace with a durable store in production) ---
// The token store must survive restarts: a NullTokenStore loses all IP mappings on restart,
// making past audit records unresolvable during security investigations.
builder.Services.AddTokenStore<NullTokenStore>();

// --- SensitiveFlow: EF Core interceptor + ASP.NET Core audit context ---
builder.Services.AddSensitiveFlowEFCore();
builder.Services.AddSensitiveFlowAspNetCore();

// --- SensitiveFlow: structured log redaction ---
builder.Services.AddSensitiveFlowLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// UseSensitiveFlowAudit must come before UseAuthentication so the IP token
// is available for every downstream middleware and handler.
app.UseSensitiveFlowAudit();

// -----------------------------------------------------------------------
// GET /customers/{id}
// Returns a masked view of the customer — raw PII never leaves the API.
// -----------------------------------------------------------------------
app.MapGet("/customers/{id}", async (
    string id,
    IAuditStore auditStore,
    IAuditContext auditContext,
    ILogger<Program> logger) =>
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
    await auditStore.AppendAsync(new AuditRecord
    {
        DataSubjectId  = customer.DataSubjectId,
        Entity         = nameof(Customer),
        Field          = "*",
        Operation      = AuditOperation.Access,
        ActorId        = auditContext.ActorId,
        IpAddressToken = auditContext.IpAddressToken,
    });

    // The redacting logger strips [PersonalData] values before they reach any sink.
    // SF0001: this would trigger the analyzer if customer.Email were passed directly.
    logger.LogInformation("Customer {Id} accessed — email masked: {Email}",
        customer.Id,
        customer.Email.MaskEmail());

    // Return a masked DTO — raw PII never leaves the API boundary.
    // SF0002: the analyzer would flag returning customer.Email directly.
    return Results.Ok(new CustomerResponse(
        customer.Id,
        customer.Name.MaskName(),
        customer.Email.MaskEmail(),
        customer.Phone.MaskPhone()));
})
.WithName("GetCustomer");

// -----------------------------------------------------------------------
// GET /customers/{id}/audit
// Shows the audit trail for a data subject.
// -----------------------------------------------------------------------
app.MapGet("/customers/{id}/audit", async (string id, IAuditStore auditStore) =>
{
    var records = await auditStore.QueryByDataSubjectAsync(id);
    return Results.Ok(records);
})
.WithName("GetCustomerAudit");

app.Run();

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

// -----------------------------------------------------------------------
// Stubs — replace both with durable implementations in production.
// -----------------------------------------------------------------------

public sealed class NullAuditStore : IAuditStore
{
    public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<AuditRecord>> QueryAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuditRecord>>([]);

    public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AuditRecord>>([]);
}

public sealed class NullTokenStore : ITokenStore
{
    public Task<string> GetOrCreateTokenAsync(string value, CancellationToken cancellationToken = default)
        => Task.FromResult(value);

    public Task<string> ResolveTokenAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(token);
}
