using Microsoft.EntityFrameworkCore;
using QuickStart.Sample;
using SensitiveFlow.AspNetCore.EFCore.Extensions;
using SensitiveFlow.Core.Profiles;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=quickstart.db";

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlite(connectionString)
        .AddInterceptors(sp.GetRequiredService<SensitiveFlow.EFCore.Interceptors.SensitiveDataAuditInterceptor>()));

builder.Services.AddSensitiveFlowWeb(options =>
{
    options.UseProfile(SensitiveFlowProfile.Balanced);

    options.UseEfCoreStores(
        audit => audit.UseSqlite("Data Source=quickstart-audit.db"),
        tokens => tokens.UseSqlite("Data Source=quickstart-tokens.db"));

    options.EnableJsonRedaction();
    options.EnableLoggingRedaction();
    options.EnableEfCoreAudit();
    options.EnableAspNetCoreContext();
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
app.MapHealthChecks("/health");

app.MapPost("/customers", async (CreateCustomerRequest request, AppDbContext db, CancellationToken ct) =>
{
    var customer = new Customer
    {
        DataSubjectId = Guid.NewGuid().ToString(),
        Name = request.Name,
        Email = request.Email,
        TaxId = request.TaxId,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    db.Customers.Add(customer);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/customers/{customer.DataSubjectId}", customer);
});

app.MapGet("/customers/{id}", async (string id, AppDbContext db, CancellationToken ct) =>
{
    var customer = await db.Customers.FirstOrDefaultAsync(c => c.DataSubjectId == id, ct);
    return customer is null ? Results.NotFound() : Results.Ok(customer);
});

app.Run();

public sealed record CreateCustomerRequest(string Name, string Email, string TaxId);
