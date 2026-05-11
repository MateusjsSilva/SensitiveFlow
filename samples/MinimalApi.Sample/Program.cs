using Microsoft.EntityFrameworkCore;
using MinimalApi.Sample.Infrastructure;
using SensitiveFlow.AspNetCore.EFCore.Extensions;
using SensitiveFlow.Core.Profiles;

var builder = WebApplication.CreateBuilder(args);

var appConnection = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=sensitiveflow-minimalapi.db";
var auditConnection = builder.Configuration.GetConnectionString("Audit")
    ?? "Data Source=sensitiveflow-minimalapi-audit.db";
var tokenConnection = builder.Configuration.GetConnectionString("Tokens")
    ?? "Data Source=sensitiveflow-minimalapi-tokens.db";

builder.Services.AddDbContext<SampleDbContext>((sp, options) =>
    options.UseSqlite(appConnection)
        .AddInterceptors(sp.GetRequiredService<SensitiveFlow.EFCore.Interceptors.SensitiveDataAuditInterceptor>()));

builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);

    options.UseEfCoreStores(
        audit => audit.UseSqlite(auditConnection),
        tokens => tokens.UseSqlite(tokenConnection));

    options.EnableEfCoreAudit();
    options.EnableAspNetCoreContext();
    options.EnableJsonRedaction();
    options.EnableLoggingRedaction();
    options.EnableValidation();
    options.EnableHealthChecks();
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseSensitiveFlow();
app.MapHealthChecks("/health/sensitiveflow");

app.MapPost("/customers", async (
    CreateCustomerRequest request,
    SampleDbContext db,
    CancellationToken ct) =>
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

    db.Customers.Add(customer);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/customers/{customer.DataSubjectId}", ToResponse(customer));
})
.WithName("CreateCustomer");

app.MapGet("/customers/{id}", async (
    string id,
    SampleDbContext db,
    CancellationToken ct) =>
{
    var customer = await db.Customers
        .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

    return customer is null ? Results.NotFound() : Results.Ok(customer);
})
.WithName("GetCustomer");

app.MapGet("/customers/{id}/json", async (
    string id,
    SampleDbContext db,
    CancellationToken ct) =>
{
    var customer = await db.Customers
        .FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);

    return customer is null ? Results.NotFound() : Results.Ok(customer);
})
.WithName("GetCustomerWithJsonRedaction");

app.Run();

static CustomerResponse ToResponse(Customer c) => new(
    c.DataSubjectId,
    c.Name,
    c.Email,
    c.Phone);

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
